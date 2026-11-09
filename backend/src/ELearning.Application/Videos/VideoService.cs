using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Questions;
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

    /// <summary>Tiêu đề / ảnh bìa của video YouTube để tự điền form bài giảng (không lưu gì).</summary>
    Task<Result<YoutubeVideoInfo>> GetYoutubeInfoAsync(string? url, CancellationToken ct);
}

internal sealed class VideoService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateVideoRequest> createValidator,
    IValidator<UpdateVideoRequest> updateValidator,
    IYoutubeMetadataClient youtube) : IVideoService
{
    private static readonly Error VideoNotFound = Error.NotFound("VIDEO_NOT_FOUND", "Không tìm thấy video bài giảng.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<VideoLessonDto>> ListAsync(VideoListQuery query, CancellationToken ct)
    {
        var videos = db.VideoLessons.AsNoTracking();

        if (query.CategoryId is { } categoryId)
        {
            var family = await db.CategoryFamilyIdsAsync(categoryId, ct);
            videos = videos.Where(v => v.CategoryId != null && family.Contains(v.CategoryId.Value));
        }

        if (query.ClassroomId is { } classroomId)
        {
            videos = videos.Where(v => v.Classrooms.Any(c => c.ClassroomId == classroomId));
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

        var items = await ToDto(videos.OrderBy(v => v.DisplayOrder).ThenByDescending(v => v.CreatedAt))
            .Skip(query.Skip).Take(query.PageSize).ToListAsync(ct);

        return new PagedResult<VideoLessonDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<VideoLessonDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var item = await ToDto(db.VideoLessons.AsNoTracking().Where(v => v.Id == id)).SingleOrDefaultAsync(ct);

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

        var classroomIds = (request.ClassroomIds ?? []).Distinct().ToList();
        if (await ClassroomsMissingAsync(classroomIds, ct))
        {
            return Error.Validation("CLASSROOM_NOT_FOUND", "Có lớp học không tồn tại.", "classroomIds");
        }

        var video = new VideoLesson(
            request.Title,
            request.VideoUrl,
            request.DocumentUrl,
            request.Description,
            request.CategoryId,
            classroomIds,
            request.DurationMinutes,
            request.DisplayOrder,
            currentUser.RequiredUserId,
            Now);

        db.VideoLessons.Add(video);
        audit.Write("Video.Created", nameof(VideoLesson), video.Id, newValue: new { video.Title, video.VideoUrl, video.DocumentUrl, ClassroomIds = classroomIds });
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

        var video = await db.VideoLessons.Include(v => v.Classrooms).SingleOrDefaultAsync(v => v.Id == id, ct);
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

        var classroomIds = (request.ClassroomIds ?? []).Distinct().ToList();
        if (await ClassroomsMissingAsync(classroomIds, ct))
        {
            return Error.Validation("CLASSROOM_NOT_FOUND", "Có lớp học không tồn tại.", "classroomIds");
        }

        video.Update(
            request.Title,
            request.VideoUrl,
            request.DocumentUrl,
            request.Description,
            request.CategoryId,
            classroomIds,
            request.DurationMinutes,
            request.DisplayOrder,
            request.IsActive,
            currentUser.RequiredUserId,
            Now);

        audit.Write("Video.Updated", nameof(VideoLesson), video.Id, newValue: new { video.Title, video.VideoUrl, video.DocumentUrl, ClassroomIds = classroomIds });
        await db.SaveChangesAsync(ct);

        return await GetAsync(video.Id, ct);
    }

    public async Task<Result<YoutubeVideoInfo>> GetYoutubeInfoAsync(string? url, CancellationToken ct)
    {
        if (VideoLesson.TryExtractYoutubeId(url) is not { } videoId)
        {
            return Error.Validation("INVALID_YOUTUBE_URL", "Không nhận ra link YouTube.", "url");
        }

        return await youtube.GetAsync(videoId, ct) is { } info
            ? info
            : Error.Business(
                "YOUTUBE_INFO_UNAVAILABLE",
                "Không lấy được thông tin video từ YouTube (video riêng tư / đã xoá, hoặc máy chủ không kết nối được YouTube). Hãy tự nhập tiêu đề.");
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        // Nạp các dòng lớp để EF xóa kèm (ClientCascade)
        var video = await db.VideoLessons.Include(v => v.Classrooms).SingleOrDefaultAsync(v => v.Id == id, ct);
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

        // Học sinh chỉ xem video active, và hoặc là video chung (không gắn lớp nào) hoặc video gắn với một lớp học sinh tham gia
        var videos = db.VideoLessons.AsNoTracking()
            .Where(v => v.IsActive && (!v.Classrooms.Any() || v.Classrooms.Any(c => myClassroomIds.Contains(c.ClassroomId))));

        if (query.CategoryId is { } categoryId)
        {
            var family = await db.CategoryFamilyIdsAsync(categoryId, ct);
            videos = videos.Where(v => v.CategoryId != null && family.Contains(v.CategoryId.Value));
        }

        if (query.ClassroomId is { } classroomId)
        {
            videos = videos.Where(v => v.Classrooms.Any(c => c.ClassroomId == classroomId));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            videos = videos.Where(v => EF.Functions.ILike(v.Title, kw) || (v.Description != null && EF.Functions.ILike(v.Description, kw)));
        }

        var total = await videos.CountAsync(ct);

        var items = await ToDto(videos.OrderBy(v => v.DisplayOrder).ThenByDescending(v => v.CreatedAt))
            .Skip(query.Skip).Take(query.PageSize).ToListAsync(ct);

        return new PagedResult<VideoLessonDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Có id lớp nào không tồn tại.</summary>
    private async Task<bool> ClassroomsMissingAsync(List<Guid> classroomIds, CancellationToken ct) =>
        classroomIds.Count > 0 && await db.Classrooms.CountAsync(c => classroomIds.Contains(c.Id), ct) != classroomIds.Count;

    /// <summary>Video → DTO kèm tên chuyên đề và danh sách lớp được xem (sắp theo mã lớp).</summary>
    private IQueryable<VideoLessonDto> ToDto(IQueryable<VideoLesson> videos) =>
        from v in videos
        join c in db.QuestionCategories.AsNoTracking() on v.CategoryId equals c.Id into cats
        from c in cats.DefaultIfEmpty()
        select new VideoLessonDto(
            v.Id,
            v.Title,
            v.VideoUrl,
            v.DocumentUrl,
            v.YoutubeVideoId,
            v.ThumbnailUrl,
            v.Description,
            v.CategoryId,
            c != null ? c.Name : null,
            (from link in v.Classrooms
             join cl in db.Classrooms on link.ClassroomId equals cl.Id
             orderby cl.Code
             select new VideoClassroomDto(cl.Id, cl.Code, cl.Name)).ToList(),
            v.DurationMinutes,
            v.DisplayOrder,
            v.IsActive,
            v.CreatedAt,
            v.UpdatedAt,
            v.RowVersion.ToBase64());
}
