using ELearning.Application.Attempts;
using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Attempts;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Videos;

public interface IVideoPracticeService
{
    /// <summary>Học viên bấm "Luyện đề" ở video (D-38).</summary>
    Task<Result<VideoPracticeDto>> StartAsync(Guid videoId, StartVideoPracticeRequest request, CancellationToken ct);
}

/// <summary>
/// "Luyện đề" từ video bài giảng (D-38): đưa đề luyện của video vào danh sách bài tập của học viên (gán trực tiếp nếu đề
/// giới hạn người thi mà học viên chưa được gán). Học viên đã làm đề thì giao diện hỏi làm lại; làm lại = hủy các lượt cũ
/// của chính học viên (trạng thái CANCELLED, không xóa cứng — D-16) rồi làm lượt mới. Lượt tự hủy không hiện cho học viên.
/// </summary>
internal sealed class VideoPracticeService(
    IAppDbContext db,
    IAttemptFinalizer finalizer,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time) : IVideoPracticeService
{
    public const string RestartReason = "Học viên làm lại đề luyện từ video bài giảng";

    private static readonly Error VideoNotFound = Error.NotFound("VIDEO_NOT_FOUND", "Không tìm thấy video bài giảng.");

    public async Task<Result<VideoPracticeDto>> StartAsync(Guid videoId, StartVideoPracticeRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var myClassroomIds = await db.ClassroomStudents.AsNoTracking()
            .Where(s => s.UserId == userId && db.Classrooms.Any(c => c.Id == s.ClassroomId && c.IsActive))
            .Select(s => s.ClassroomId)
            .ToListAsync(ct);

        // Chỉ video học viên được xem (đang bật; bài chung hoặc gán cho lớp của học viên) — khác thì 404 (quy tắc 10)
        var video = await db.VideoLessons.AsNoTracking()
            .Where(v => v.Id == videoId && v.IsActive
                && (!v.Classrooms.Any() || v.Classrooms.Any(c => myClassroomIds.Contains(c.ClassroomId))))
            .Select(v => new { v.Id, v.PracticeExamId })
            .SingleOrDefaultAsync(ct);
        if (video is null)
        {
            return VideoNotFound;
        }

        if (video.PracticeExamId is not { } examId)
        {
            return Error.NotFound("PRACTICE_EXAM_NOT_SET", "Video này chưa có đề luyện.");
        }

        var exam = await db.Exams.Include(e => e.Assignments).SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return Error.NotFound("PRACTICE_EXAM_NOT_SET", "Video này chưa có đề luyện.");
        }

        if (exam.Status != ExamStatus.Published)
        {
            return Error.Business("PRACTICE_EXAM_NOT_OPEN", "Đề luyện của video chưa mở hoặc đã đóng. Hãy báo giáo viên.");
        }

        var previous = await db.ExamAttempts.AsNoTracking()
            .Where(a => a.ExamId == examId && a.UserId == userId && a.Status != AttemptStatus.Cancelled)
            .Select(a => a.Id)
            .ToListAsync(ct);
        if (previous.Count > 0 && !request.Restart)
        {
            return new VideoPracticeDto(exam.Id, exam.Code, exam.Name, AlreadyAttempted: true, previous.Count, Ready: false);
        }

        var groupIds = await db.UserGroupMembers.AsNoTracking()
            .Where(m => m.UserId == userId && db.UserGroups.Any(g => g.Id == m.GroupId && g.IsActive))
            .Select(m => m.GroupId)
            .ToListAsync(ct);
        var canSee = exam.AccessMode == AccessMode.Public
            || exam.Assignments.Any(a => a.UserId == userId
                || (a.GroupId is { } g && groupIds.Contains(g))
                || (a.ClassroomId is { } c && myClassroomIds.Contains(c)));
        var now = time.GetUtcNow().UtcDateTime;

        await db.InTransactionAsync(
            async () =>
            {
                // Làm lại: hủy mọi lượt cũ của chính học viên; lượt đang làm thì khóa dòng trước khi đổi trạng thái
                foreach (var attemptId in previous)
                {
                    var attempt = await finalizer.LoadForUpdateAsync(attemptId, ct);
                    if (attempt is null || attempt.Status == AttemptStatus.Cancelled)
                    {
                        continue;
                    }

                    var oldStatus = attempt.Status;
                    attempt.Cancel(userId, RestartReason, now);
                    audit.Write(AuditActions.AttemptCancelled, nameof(ExamAttempt), attempt.Id, new { Status = oldStatus }, new { attempt.Status }, RestartReason);
                }

                // Đưa đề vào danh sách bài tập của học viên: gán trực tiếp khi đề giới hạn người thi mà học viên chưa thấy
                if (!canSee)
                {
                    exam.SetAssignments(
                        exam.Assignments.Where(a => a.GroupId is not null).Select(a => a.GroupId!.Value).ToList(),
                        [.. exam.Assignments.Where(a => a.UserId is not null).Select(a => a.UserId!.Value), userId],
                        userId,
                        now,
                        exam.Assignments.Where(a => a.ClassroomId is not null).Select(a => a.ClassroomId!.Value).ToList());
                    audit.Write(AuditActions.ExamAssignmentsChanged, nameof(Exam), exam.Id, newValue: new { AddedUserId = userId, VideoId = video.Id });
                }

                await db.SaveChangesAsync(ct);
                return true;
            },
            ct);

        return new VideoPracticeDto(exam.Id, exam.Code, exam.Name, AlreadyAttempted: previous.Count > 0, previous.Count, Ready: true);
    }
}
