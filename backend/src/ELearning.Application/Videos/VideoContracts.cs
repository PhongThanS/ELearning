using ELearning.Shared.Paging;
using FluentValidation;

namespace ELearning.Application.Videos;

public sealed record VideoLessonDto(
    Guid Id,
    string Title,
    string VideoUrl,
    string? DocumentUrl,
    string? YoutubeVideoId,
    string? ThumbnailUrl,
    string? Description,
    Guid? CategoryId,
    string? CategoryName,
    Guid? ClassroomId,
    string? ClassroomName,
    int? DurationMinutes,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion);

public sealed record CreateVideoRequest(
    string Title,
    string VideoUrl,
    string? Description = null,
    Guid? CategoryId = null,
    Guid? ClassroomId = null,
    int? DurationMinutes = null,
    int DisplayOrder = 0,
    string? DocumentUrl = null);

public sealed record UpdateVideoRequest(
    string Title,
    string VideoUrl,
    string? Description,
    Guid? CategoryId,
    Guid? ClassroomId,
    int? DurationMinutes,
    int DisplayOrder,
    bool IsActive,
    string RowVersion,
    string? DocumentUrl = null);

public sealed record VideoListQuery : PageRequest
{
    public Guid? CategoryId { get; init; }
    public Guid? ClassroomId { get; init; }
    public string? Keyword { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class CreateVideoRequestValidator : AbstractValidator<CreateVideoRequest>
{
    public CreateVideoRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().WithMessage("Tiêu đề video không được để trống.")
            .MaximumLength(250).WithMessage("Tiêu đề không vượt quá 250 ký tự.");
        RuleFor(r => r.VideoUrl).NotEmpty().WithMessage("Đường dẫn video không được để trống.")
            .MaximumLength(1000).WithMessage("Đường dẫn video không vượt quá 1000 ký tự.");
        RuleFor(r => r.DocumentUrl).MaximumLength(1000).WithMessage("Đường dẫn tài liệu không vượt quá 1000 ký tự.")
            .Must(url => VideoLinks.IsGoogleDocument(url!)).When(r => !string.IsNullOrWhiteSpace(r.DocumentUrl))
            .WithErrorCode("INVALID_DOCUMENT_URL").WithMessage(VideoLinks.DocumentUrlMessage);
        RuleFor(r => r.Description).MaximumLength(4000).WithMessage("Mô tả không vượt quá 4000 ký tự.");
        RuleFor(r => r.DurationMinutes).GreaterThanOrEqualTo(0).When(r => r.DurationMinutes.HasValue)
            .WithMessage("Thời lượng video phải lớn hơn hoặc bằng 0.");
    }
}

public sealed class UpdateVideoRequestValidator : AbstractValidator<UpdateVideoRequest>
{
    public UpdateVideoRequestValidator()
    {
        RuleFor(r => r.Title).NotEmpty().WithMessage("Tiêu đề video không được để trống.")
            .MaximumLength(250).WithMessage("Tiêu đề không vượt quá 250 ký tự.");
        RuleFor(r => r.VideoUrl).NotEmpty().WithMessage("Đường dẫn video không được để trống.")
            .MaximumLength(1000).WithMessage("Đường dẫn video không vượt quá 1000 ký tự.");
        RuleFor(r => r.DocumentUrl).MaximumLength(1000).WithMessage("Đường dẫn tài liệu không vượt quá 1000 ký tự.")
            .Must(url => VideoLinks.IsGoogleDocument(url!)).When(r => !string.IsNullOrWhiteSpace(r.DocumentUrl))
            .WithErrorCode("INVALID_DOCUMENT_URL").WithMessage(VideoLinks.DocumentUrlMessage);
        RuleFor(r => r.Description).MaximumLength(4000).WithMessage("Mô tả không vượt quá 4000 ký tự.");
        RuleFor(r => r.DurationMinutes).GreaterThanOrEqualTo(0).When(r => r.DurationMinutes.HasValue)
            .WithMessage("Thời lượng video phải lớn hơn hoặc bằng 0.");
        RuleFor(r => r.RowVersion).NotEmpty().WithMessage("Thiếu rowVersion.");
    }
}

/// <summary>Tài liệu đi kèm bài giảng phải là link Google Drive / Docs (trình duyệt chỉ cho nhúng các trang này, xem CSP frame-src).</summary>
public static class VideoLinks
{
    public const string DocumentUrlMessage =
        "Link tài liệu phải là link Google Drive (https://drive.google.com/file/d/...) hoặc Google Docs, chia sẻ \"Bất kỳ ai có đường liên kết\".";

    public static bool IsGoogleDocument(string url) =>
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host is "drive.google.com" or "docs.google.com";
}
