using System.ComponentModel.DataAnnotations;

namespace ELearning.Application.Common.Options;

/// <summary>Cấu hình tính giờ và job tự nộp (D-05). Section "Exam".</summary>
public sealed class ExamOptions
{
    public const string SectionName = "Exam";

    /// <summary>Ân hạn cho request lưu/nộp đến trễ do mạng.</summary>
    [Range(0, 300)]
    public int SubmitGraceSeconds { get; init; } = 30;

    [Range(5, 3600)]
    public int ExpirationSweepIntervalSeconds { get; init; } = 60;

    [Range(1, 1000)]
    public int ExpirationSweepBatchSize { get; init; } = 100;

    /// <summary>Tắt job nền (ví dụ trong test, hoặc khi chỉ một instance chạy job).</summary>
    public bool ExpirationWorkerEnabled { get; init; } = true;
}
