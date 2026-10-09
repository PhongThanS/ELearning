using System.Text.RegularExpressions;
using ELearning.Domain.Classes;
using ELearning.Domain.Common;
using ELearning.Domain.Questions;

namespace ELearning.Domain.Videos;

/// <summary>
/// Video bài giảng / tài liệu học tập (chỉ lưu link nhúng như YouTube, Vimeo, hoặc URL trực tiếp).
/// Phân loại theo Chuyên đề (Category) hoặc Lớp học (Classroom).
/// </summary>
public sealed class VideoLesson : Entity, IHasRowVersion
{
    public const int MaxTitleLength = 250;
    public const int MaxUrlLength = 1000;
    public const int MaxDescriptionLength = 4000;

    private static readonly Regex YoutubeRegex = new(
        @"(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=|shorts\/))([\w-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private VideoLesson()
    {
    }

    public VideoLesson(
        string title,
        string videoUrl,
        string? description,
        Guid? categoryId,
        Guid? classroomId,
        int? durationMinutes,
        int displayOrder,
        Guid createdBy,
        DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoUrl);

        Title = title.Trim();
        VideoUrl = videoUrl.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryId = categoryId;
        ClassroomId = classroomId;
        DurationMinutes = durationMinutes;
        DisplayOrder = displayOrder;
        IsActive = true;
        CreatedBy = createdBy;
        CreatedAt = createdAt;

        ApplyYoutubeExtraction();
    }

    public string Title { get; private set; } = null!;

    public string VideoUrl { get; private set; } = null!;

    /// <summary>ID video YouTube (11 ký tự) để sinh iframe nhúng trực tiếp trên web.</summary>
    public string? YoutubeVideoId { get; private set; }

    /// <summary>Ảnh thumbnail xem trước video (tự động lấy từ YouTube nếu có).</summary>
    public string? ThumbnailUrl { get; private set; }

    public string? Description { get; private set; }

    public Guid? CategoryId { get; private set; }

    public QuestionCategory? Category { get; private set; }

    public Guid? ClassroomId { get; private set; }

    public Classroom? Classroom { get; private set; }

    public int? DurationMinutes { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Update(
        string title,
        string videoUrl,
        string? description,
        Guid? categoryId,
        Guid? classroomId,
        int? durationMinutes,
        int displayOrder,
        bool isActive,
        Guid updatedBy,
        DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(videoUrl);

        Title = title.Trim();
        VideoUrl = videoUrl.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryId = categoryId;
        ClassroomId = classroomId;
        DurationMinutes = durationMinutes;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;

        ApplyYoutubeExtraction();
    }

    public void SetActive(bool isActive, Guid updatedBy, DateTime now)
    {
        IsActive = isActive;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    private void ApplyYoutubeExtraction()
    {
        var match = YoutubeRegex.Match(VideoUrl);
        if (match.Success)
        {
            YoutubeVideoId = match.Groups[1].Value;
            ThumbnailUrl = $"https://img.youtube.com/vi/{YoutubeVideoId}/hqdefault.jpg";
        }
        else if (VideoUrl.Length == 11 && Regex.IsMatch(VideoUrl, @"^[\w-]{11}$"))
        {
            YoutubeVideoId = VideoUrl;
            ThumbnailUrl = $"https://img.youtube.com/vi/{YoutubeVideoId}/hqdefault.jpg";
            VideoUrl = $"https://www.youtube.com/watch?v={YoutubeVideoId}";
        }
        else
        {
            YoutubeVideoId = null;
            ThumbnailUrl = null;
        }
    }
}
