using System.ComponentModel.DataAnnotations;

namespace ELearning.Application.Common.Options;

/// <summary>Ngưỡng cảnh báo và chu kỳ ghi chỉ số (docs/09-van-hanh.md mục 7). Section "Monitoring".</summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>Cửa sổ tính tỉ lệ 5xx.</summary>
    [Range(1, 60)]
    public int ErrorRateWindowMinutes { get; init; } = 5;

    /// <summary>Cảnh báo khi tỉ lệ 5xx trong cửa sổ vượt ngưỡng này (%).</summary>
    [Range(0.01, 100)]
    public double ErrorRatePercent { get; init; } = 1;

    /// <summary>Ít request hơn thì không cảnh báo tỉ lệ (1 lỗi / 10 request lúc vắng không phải sự cố).</summary>
    [Range(1, 100_000)]
    public int ErrorRateMinRequests { get; init; } = 50;

    /// <summary>Cảnh báo khi có lượt thi quá hạn lâu hơn chừng này mà chưa được nộp.</summary>
    [Range(1, 120)]
    public int OverdueAttemptMinutes { get; init; } = 5;

    /// <summary>Chu kỳ ghi dòng log "Monitoring" tổng hợp chỉ số.</summary>
    [Range(10, 3600)]
    public int SnapshotIntervalSeconds { get; init; } = 60;

    /// <summary>Tắt job ghi chỉ số (ví dụ trong test).</summary>
    public bool SnapshotEnabled { get; init; } = true;
}
