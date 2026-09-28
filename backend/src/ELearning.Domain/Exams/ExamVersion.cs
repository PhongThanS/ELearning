using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.Domain.Exams;

/// <summary>
/// Phiên bản đề: cấu hình chấm điểm / tính giờ / hiển thị, bất biến sau khi publish (D-03, D-04).
/// </summary>
public sealed class ExamVersion : Entity, IHasRowVersion
{
    private readonly List<ExamQuestion> _questions = [];

    private ExamVersion()
    {
    }

    public Guid ExamId { get; private set; }

    public int VersionNumber { get; private set; }

    public ExamVersionStatus Status { get; private set; }

    public int DurationMinutes { get; private set; }

    public decimal? PassPercentage { get; private set; }

    public ScoreVisibility ScoreVisibility { get; private set; }

    public ReviewPolicy ReviewPolicy { get; private set; }

    public bool ShuffleQuestions { get; private set; }

    public bool ShuffleOptions { get; private set; }

    public int? QuestionCount { get; private set; }

    public decimal? MaxScore { get; private set; }

    public DateTime? PublishedAt { get; private set; }

    public Guid? PublishedBy { get; private set; }

    public DateTime? ArchivedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<ExamQuestion> Questions => _questions;
}
