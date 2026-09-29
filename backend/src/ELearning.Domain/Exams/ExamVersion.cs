using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Questions;

namespace ELearning.Domain.Exams;

/// <summary>Cấu hình chấm điểm / tính giờ / hiển thị của một version (D-03, D-09).</summary>
public sealed record VersionSettings(
    int DurationMinutes,
    decimal? PassPercentage,
    ScoreVisibility ScoreVisibility,
    ReviewPolicy ReviewPolicy,
    bool ShuffleQuestions = false,
    bool ShuffleOptions = false);

/// <summary>Một lỗi chặn publish; Field để UI dẫn tới đúng bước.</summary>
public sealed record PublishIssue(string Code, string Message, string? Field = null);

/// <summary>
/// Phiên bản đề: bất biến sau khi publish (D-03, D-04).
/// Câu hỏi được copy vào ExamQuestions ngay khi thêm vào bản nháp (D-02).
/// </summary>
public sealed class ExamVersion : Entity, IHasRowVersion
{
    public const int MinDuration = 1;
    public const int MaxDuration = 600;
    public const int MaxQuestions = 500;

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

    public bool IsDraft => Status == ExamVersionStatus.Draft;

    public VersionSettings Settings => new(DurationMinutes, PassPercentage, ScoreVisibility, ReviewPolicy, ShuffleQuestions, ShuffleOptions);

    public static ExamVersion CreateDraft(Guid examId, int versionNumber, VersionSettings settings, Guid createdBy, DateTime now)
    {
        var version = new ExamVersion
        {
            ExamId = examId,
            VersionNumber = versionNumber,
            Status = ExamVersionStatus.Draft,
            CreatedBy = createdBy,
            CreatedAt = now,
        };
        version.ApplySettings(settings);
        return version;
    }

    /// <summary>Tạo bản nháp mới copy cấu hình và câu hỏi của version này (cho version mới hoặc clone đề).</summary>
    public ExamVersion CopyAsDraft(Guid examId, int versionNumber, Guid createdBy, DateTime now)
    {
        var copy = CreateDraft(examId, versionNumber, Settings, createdBy, now);
        copy.CopyQuestionsFrom(this, now);
        return copy;
    }

    /// <summary>Thay cấu hình và danh sách câu hỏi của bản nháp này bằng bản sao từ version khác.</summary>
    public void CopyFrom(ExamVersion source, DateTime now)
    {
        EnsureDraft();
        ApplySettings(source.Settings);
        _questions.Clear();
        CopyQuestionsFrom(source, now);
    }

    private void CopyQuestionsFrom(ExamVersion source, DateTime now)
    {
        foreach (var question in source._questions.OrderBy(q => q.QuestionOrder))
        {
            _questions.Add(question.CopyTo(Id, _questions.Count + 1, now));
        }
    }

    /// <summary>Đặt lại thứ tự theo danh sách Id đầy đủ (phải chứa đúng mọi câu của version).</summary>
    public void Reorder(IReadOnlyList<Guid> examQuestionIds)
    {
        EnsureDraft();
        if (examQuestionIds.Count != _questions.Count
            || examQuestionIds.Distinct().Count() != _questions.Count
            || !examQuestionIds.All(id => _questions.Exists(q => q.Id == id)))
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, "Danh sách sắp xếp phải chứa đúng và đủ các câu hỏi của phiên bản.");
        }

        for (var i = 0; i < examQuestionIds.Count; i++)
        {
            _questions.Find(q => q.Id == examQuestionIds[i])!.SetOrder(i + 1);
        }
    }

    /// <summary>Bước 1 của sắp xếp: dời mọi thứ tự ra xa để tránh vi phạm unique (docs/10-bay-ky-thuat.md mục 8).</summary>
    public void ShiftOrdersForReorder()
    {
        EnsureDraft();
        _questions.ForEach(q => q.SetOrder(q.QuestionOrder + 100_000));
    }

    public void UpdateSettings(VersionSettings settings)
    {
        EnsureDraft();
        ApplySettings(settings);
    }

    /// <summary>Snapshot câu hỏi ngân hàng vào bản nháp (D-02). Trả về null nếu câu đã có trong version.</summary>
    public ExamQuestion? AddQuestion(Question source, decimal? score, DateTime now)
    {
        EnsureDraft();
        if (_questions.Exists(q => q.SourceQuestionId == source.Id))
        {
            return null;
        }

        if (_questions.Count >= MaxQuestions)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, $"Mỗi đề tối đa {MaxQuestions} câu hỏi.");
        }

        var order = _questions.Count == 0 ? 1 : _questions.Max(q => q.QuestionOrder) + 1;
        var question = ExamQuestion.Snapshot(Id, source, order, score ?? source.DefaultScore, now);
        _questions.Add(question);
        return question;
    }

    public bool RemoveQuestion(Guid examQuestionId)
    {
        EnsureDraft();
        return _questions.RemoveAll(q => q.Id == examQuestionId) > 0;
    }

    public void SetQuestionScore(Guid examQuestionId, decimal score)
    {
        EnsureDraft();
        var question = _questions.Find(q => q.Id == examQuestionId)
            ?? throw new DomainException(DomainErrorCodes.InvalidExam, "Câu hỏi không thuộc phiên bản này.");
        question.SetScore(score);
    }

    /// <summary>Đồng bộ lại nội dung từ ngân hàng (D-02); điểm trong đề được giữ nguyên.</summary>
    public void SyncQuestion(Guid examQuestionId, Question source, DateTime now)
    {
        EnsureDraft();
        var question = _questions.Find(q => q.Id == examQuestionId)
            ?? throw new DomainException(DomainErrorCodes.InvalidExam, "Câu hỏi không thuộc phiên bản này.");
        question.RefreshFrom(source, now);
    }

    public void EnsureDraft()
    {
        if (!IsDraft)
        {
            throw new DomainException(DomainErrorCodes.VersionImmutable, "Phiên bản đã publish không thể chỉnh sửa.");
        }
    }

    /// <summary>Toàn bộ lỗi chặn publish (docs/02-nghiep-vu.md mục 4.5), không dừng ở lỗi đầu tiên.</summary>
    public IReadOnlyList<PublishIssue> ValidateForPublish(Exam exam, bool hasAssignments, DateTime now)
    {
        var issues = new List<PublishIssue>();
        if (!IsDraft)
        {
            issues.Add(new PublishIssue("VERSION_NOT_DRAFT", "Chỉ publish được phiên bản nháp."));
        }

        if (DurationMinutes is < MinDuration or > MaxDuration)
        {
            issues.Add(new PublishIssue("DURATION_INVALID", $"Thời lượng phải từ {MinDuration} đến {MaxDuration} phút.", "durationMinutes"));
        }

        if (_questions.Count == 0)
        {
            issues.Add(new PublishIssue("NO_QUESTIONS", "Đề thi chưa có câu hỏi.", "questions"));
        }
        else if (_questions.Count > MaxQuestions)
        {
            issues.Add(new PublishIssue("TOO_MANY_QUESTIONS", $"Mỗi đề tối đa {MaxQuestions} câu hỏi.", "questions"));
        }

        foreach (var question in _questions.OrderBy(q => q.QuestionOrder))
        {
            foreach (var error in Question.Validate(question.ToData()))
            {
                issues.Add(new PublishIssue("QUESTION_INVALID", $"Câu {question.QuestionOrder}: {error}", $"questions[{question.Id}]"));
            }
        }

        if (_questions.Sum(q => q.Score) <= 0 && _questions.Count > 0)
        {
            issues.Add(new PublishIssue("MAX_SCORE_ZERO", "Tổng điểm của đề phải lớn hơn 0.", "questions"));
        }

        if (exam.StartAt is { } start && exam.EndAt is { } end && start >= end)
        {
            issues.Add(new PublishIssue("SCHEDULE_INVALID", "Thời điểm bắt đầu phải trước thời điểm kết thúc.", "startAt"));
        }

        if (exam.EndAt is { } endAt && endAt <= now)
        {
            issues.Add(new PublishIssue("END_AT_IN_PAST", "Thời điểm kết thúc đã ở quá khứ.", "endAt"));
        }

        if (PassPercentage is < 0 or > 100)
        {
            issues.Add(new PublishIssue("PASS_PERCENTAGE_INVALID", "Tỉ lệ đạt phải từ 0 đến 100.", "passPercentage"));
        }

        if (exam.EndAt is null && (ReviewPolicy == ReviewPolicy.AfterExamEnd || ScoreVisibility == ScoreVisibility.AfterExamEnd))
        {
            issues.Add(new PublishIssue(
                "END_AT_REQUIRED", "Chính sách hiển thị \"sau khi đề đóng\" cần có thời điểm kết thúc.", "endAt"));
        }

        if (ReviewPolicy == ReviewPolicy.AfterSubmit && exam.MaxAttempts > 1)
        {
            issues.Add(new PublishIssue(
                "REVIEW_AFTER_SUBMIT_WITH_RETAKES",
                "Không cho xem đáp án ngay sau khi nộp khi đề cho thi nhiều lượt.",
                "reviewPolicy"));
        }

        if (exam.AccessMode == AccessMode.Assigned && !hasAssignments)
        {
            issues.Add(new PublishIssue("NO_ASSIGNMENTS", "Đề giới hạn người thi nhưng chưa gán cho nhóm hoặc người nào.", "assignments"));
        }

        return issues;
    }

    public void Publish(Guid publishedBy, DateTime now)
    {
        EnsureDraft();
        Status = ExamVersionStatus.Published;
        QuestionCount = _questions.Count;
        MaxScore = _questions.Sum(q => q.Score);
        PublishedAt = now;
        PublishedBy = publishedBy;
    }

    public void Archive(DateTime now)
    {
        if (Status != ExamVersionStatus.Published)
        {
            throw new DomainException(DomainErrorCodes.InvalidStateTransition, "Chỉ lưu trữ được phiên bản đang publish.");
        }

        Status = ExamVersionStatus.Archived;
        ArchivedAt = now;
    }

    private void ApplySettings(VersionSettings settings)
    {
        if (settings.DurationMinutes is < MinDuration or > MaxDuration)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, $"Thời lượng phải từ {MinDuration} đến {MaxDuration} phút.");
        }

        if (settings.PassPercentage is < 0 or > 100)
        {
            throw new DomainException(DomainErrorCodes.InvalidExam, "Tỉ lệ đạt phải từ 0 đến 100.");
        }

        DurationMinutes = settings.DurationMinutes;
        PassPercentage = settings.PassPercentage;
        ScoreVisibility = settings.ScoreVisibility;
        ReviewPolicy = settings.ReviewPolicy;
        ShuffleQuestions = settings.ShuffleQuestions;
        ShuffleOptions = settings.ShuffleOptions;
    }
}
