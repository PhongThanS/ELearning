using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Videos;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Videos;

public interface IVideoService
{
    Task<PagedResult<VideoLessonDto>> ListAsync(VideoListQuery query, CancellationToken ct);
    Task<Result<VideoLessonDto>> GetAsync(Guid id, CancellationToken ct);
    Task<Result<VideoLessonDto>> CreateAsync(CreateVideoRequest request, CancellationToken ct);
    Task<Result<VideoLessonDto>> UpdateAsync(Guid id, UpdateVideoRequest request, CancellationToken ct);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct);
    Task<Result<VideoLessonDto>> SetStatusAsync(Guid id, bool isActive, CancellationToken ct);
    Task<PagedResult<VideoLessonDto>> ListForStudentAsync(VideoListQuery query, CancellationToken ct);
}

internal sealed class VideoService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateVideoRequest> createValidator,
    IValidator<UpdateVideoRequest> updateValidator) : IVideoService
{
    private static readonly Error VideoNotFound = Error.NotFound("VIDEO_NOT_FOUND", "Không tìm thấy video bài giảng.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<VideoLessonDto>> ListAsync(VideoListQuery query, CancellationToken ct)
    {
        var videos = db.VideoLessons.AsNoTracking();

        if (query.CategoryId is { } categoryId)
        {
            videos = videos.Where(v => v.CategoryId == categoryId);
        }

        if (query.ClassroomId is { } classroomId)
        {
            videos = videos.Where(v => v.ClassroomId == classroomId);
        }

        if (query.IsActive is { } isActive)
        {
            videos = videos.Where(v => v.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            videos = videos.Where(v => EF.Functions.ILike(v.Title, kw) || (v.Description != null && EF.Functions.ILike(v.Description, kw)));
        }

        var total = await videos.CountAsync(ct);

        var items = await (
            from v in videos
            join c in db.QuestionCategories.AsNoTracking() on v.CategoryId equals c.Id into cats
            from c in cats.DefaultIfEmpty()
            join cl in db.Classrooms.AsNoTracking() on v.ClassroomId equals cl.Id into cls
            from cl in cls.DefaultIfEmpty()
            orderby v.DisplayOrder, v.CreatedAt descending
            select new VideoLessonDto(
                v.Id,
                v.Title,
                v.VideoUrl,
                v.YoutubeVideoId,
                v.ThumbnailUrl,
                v.Description,
                v.CategoryId,
                c != null ? c.Name : null,
                v.ClassroomId,
                cl != null ? cl.Name : null,
                v.DurationMinutes,
                v.DisplayOrder,
                v.IsActive,
                v.CreatedAt,
                v.UpdatedAt,
                v.RowVersion.ToBase64())
        ).Skip(query.Skip).Take(query.PageSize).ToListAsync(ct);

        return new PagedResult<VideoLessonDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<VideoLessonDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await (
            from v in db.VideoLessons.AsNoTracking()
            where v.Id == id
            join c in db.QuestionCategories.AsNoTracking() on v.CategoryId equals c.Id into cats
            from c in cats.DefaultIfEmpty()
            join cl in db.Classrooms.AsNoTracking() on v.ClassroomId equals cl.Id into cls
            from cl in cls.DefaultIfEmpty()
            select new VideoLessonDto(
                v.Id,
                v.Title,
                v.VideoUrl,
                v.YoutubeVideoId,
                v.ThumbnailUrl,
                v.Description,
                v.CategoryId,
                c != null ? c.Name : null,
                v.ClassroomId,
                cl != null ? cl.Name : null,
                v.DurationMinutes,
                v.DisplayOrder,
                v.IsActive,
                v.CreatedAt,
                v.UpdatedAt,
                v.RowVersion.ToBase64())
        ).SingleOrDefaultAsync(ct);

        return item is null ? VideoNotFound : item;
    }

    public async Task<Result<VideoLessonDto>> CreateAsync(CreateVideoRequest request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<VideoLessonDto>.Failure(errors);
        }

        if (request.CategoryId is { } catId && !await db.QuestionCategories.AnyAsync(c => c.Id == catId, ct))
        {
            return Error.Validation("CATEGORY_NOT_FOUND", "Chuyên đề không tồn tại.", "categoryId");
        }

        if (request.ClassroomId is { } classId && !await db.Classrooms.AnyAsync(c => c.Id == classId, ct))
        {
            return Error.Validation("CLASSROOM_NOT_FOUND", "Lớp học không tồn tại.", "classroomId");
        }

        var video = new VideoLesson(
            request.Title,
            request.VideoUrl,
            request.Description,
            request.CategoryId,
            request.ClassroomId,
            request.DurationMinutes,
            request.DisplayOrder,
            currentUser.RequiredUserId,
            Now);

        db.VideoLessons.Add(video);
        audit.Write("Video.Created", nameof(VideoLesson), video.Id, newValue: new { video.Title, video.VideoUrl });
        await db.SaveChangesAsync(ct);

        return await GetAsync(video.Id, ct);
    }

    public async Task<Result<VideoLessonDto>> UpdateAsync(Guid id, UpdateVideoRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<VideoLessonDto>.Failure(errors);
        }

        var video = await db.VideoLessons.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (video is null)
        {
            return VideoNotFound;
        }

        if (!db.TryApplyRowVersion(video, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        if (request.CategoryId is { } catId && !await db.QuestionCategories.AnyAsync(c => c.Id == catId, ct))
        {
            return Error.Validation("CATEGORY_NOT_FOUND", "Chuyên đề không tồn tại.", "categoryId");
        }

        if (request.ClassroomId is { } classId && !await db.Classrooms.AnyAsync(c => c.Id == classId, ct))
        {
            return Error.Validation("CLASSROOM_NOT_FOUND", "Lớp học không tồn tại.", "classroomId");
        }

        video.Update(
            request.Title,
            request.VideoUrl,
            request.Description,
            request.CategoryId,
            request.ClassroomId,
            request.DurationMinutes,
            request.DisplayOrder,
            request.IsActive,
            currentUser.RequiredUserId,
            Now);

        audit.Write("Video.Updated", nameof(VideoLesson), video.Id, newValue: new { video.Title, video.VideoUrl });
        await db.SaveChangesAsync(ct);

        return await GetAsync(video.Id, ct);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var video = await db.VideoLessons.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (video is null)
        {
            return VideoNotFound;
        }

        db.VideoLessons.Remove(video);
        audit.Write("Video.Deleted", nameof(VideoLesson), video.Id, new { video.Title, video.VideoUrl });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<VideoLessonDto>> SetStatusAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var video = await db.VideoLessons.SingleOrDefaultAsync(v => v.Id == id, ct);
        if (video is null)
        {
            return VideoNotFound;
        }

        if (video.IsActive != isActive)
        {
            video.SetActive(isActive, currentUser.RequiredUserId, Now);
            audit.Write("Video.StatusChanged", nameof(VideoLesson), video.Id, newValue: new { IsActive = isActive });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(video.Id, ct);
    }

    public async Task<PagedResult<VideoLessonDto>> ListForStudentAsync(VideoListQuery query, CancellationToken ct)
    {
        var userId = currentUser.RequiredUserId;
        var myClassroomIds = await db.ClassroomStudents.AsNoTracking()
            .Where(cs => cs.UserId == userId)
            .Select(cs => cs.ClassroomId)
            .ToListAsync(ct);

        // Học sinh chỉ xem video active, và hoặc là video chung (ClassroomId == null) hoặc video thuộc lớp học sinh tham gia
        var videos = db.VideoLessons.AsNoTracking()
            .Where(v => v.IsActive && (v.ClassroomId == null || myClassroomIds.Contains(v.ClassroomId.Value)));

        if (query.CategoryId is { } categoryId)
        {
            videos = videos.Where(v => v.CategoryId == categoryId);
        }

        if (query.ClassroomId is { } classroomId)
        {
            videos = videos.Where(v => v.ClassroomId == classroomId);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            videos = videos.Where(v => EF.Functions.ILike(v.Title, kw) || (v.Description != null && EF.Functions.ILike(v.Description, kw)));
        }

        var total = await videos.CountAsync(ct);

        var items = await (
            from v in videos
            join c in db.QuestionCategories.AsNoTracking() on v.CategoryId equals c.Id into cats
            from c in cats.DefaultIfEmpty()
            join cl in db.Classrooms.AsNoTracking() on v.ClassroomId equals cl.Id into cls
            from cl in cls.DefaultIfEmpty()
            orderby v.DisplayOrder, v.CreatedAt descending
            select new VideoLessonDto(
                v.Id,
                v.Title,
                v.VideoUrl,
                v.YoutubeVideoId,
                v.ThumbnailUrl,
                v.Description,
                v.CategoryId,
                c != null ? c.Name : null,
                v.ClassroomId,
                cl != null ? cl.Name : null,
                v.DurationMinutes,
                v.DisplayOrder,
                v.IsActive,
                v.CreatedAt,
                v.UpdatedAt,
                v.RowVersion.ToBase64())
        ).Skip(query.Skip).Take(query.PageSize).ToListAsync(ct);

        return new PagedResult<VideoLessonDto>(items, query.Page, query.PageSize, total);
    }
}
