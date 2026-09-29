using ELearning.Application.Common.Abstractions;
using ELearning.Application.Monitoring;
using ELearning.Domain.Attempts;
using ELearning.Domain.Exams;
using ELearning.Domain.Grading;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Grading;

public interface IGradingService
{
    /// <summary>
    /// Chấm mọi câu của lượt thi (đã tải Questions → Answer → SelectedOptions), ghi điểm từng câu
    /// và trả về tổng kết. Chỉ đọc đáp án từ snapshot ExamQuestions (D-01).
    /// </summary>
    Task<AttemptScore> GradeAsync(ExamAttempt attempt, CancellationToken ct);
}

/// <summary>Chỉ biết dữ liệu chấm; không biết HTTP, JWT, UI (docs/03-kien-truc.md mục 4).</summary>
internal sealed class GradingService(IAppDbContext db, GradingEngine engine, TimeProvider time, OperationalMetrics metrics) : IGradingService
{
    public async Task<AttemptScore> GradeAsync(ExamAttempt attempt, CancellationToken ct)
    {
        try
        {
            return await GradeCoreAsync(attempt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Đếm để cảnh báo (docs/09-van-hanh.md mục 7); lỗi vẫn được ném tiếp cho transaction rollback.
            metrics.RecordGradingFailure();
            throw;
        }
    }

    private async Task<AttemptScore> GradeCoreAsync(ExamAttempt attempt, CancellationToken ct)
    {
        var version = await db.ExamVersions.AsNoTracking().SingleAsync(v => v.Id == attempt.ExamVersionId, ct);
        var questions = await LoadSnapshotAsync(attempt.ExamVersionId, ct);
        var now = time.GetUtcNow().UtcDateTime;

        var graded = new List<(GradingResult Result, bool IsAnswered)>(attempt.Questions.Count);
        foreach (var attemptQuestion in attempt.Questions.OrderBy(q => q.QuestionOrder))
        {
            var snapshot = questions[attemptQuestion.ExamQuestionId];
            var answer = attemptQuestion.Answer.ToStudentAnswer();
            var result = engine.Grade(GradingQuestion.From(snapshot), answer);
            attemptQuestion.Answer.Grade(result, now);
            graded.Add((result, answer.IsAnswered));
        }

        return AttemptScore.Calculate(graded, version.PassPercentage);
    }

    private async Task<Dictionary<Guid, ExamQuestion>> LoadSnapshotAsync(Guid versionId, CancellationToken ct) =>
        await db.ExamQuestions.AsNoTracking()
            .Include(q => q.Options)
            .Include(q => q.AcceptedAnswers)
            .Where(q => q.ExamVersionId == versionId)
            .ToDictionaryAsync(q => q.Id, ct);
}
