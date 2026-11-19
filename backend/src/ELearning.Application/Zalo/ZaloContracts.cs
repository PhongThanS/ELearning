using ELearning.Domain.Enums;
using ELearning.Shared.Results;

namespace ELearning.Application.Zalo;

/// <summary>Một tin gửi tới Zalo: số điện thoại đã chuẩn hóa, mã mẫu, dữ liệu điền vào mẫu.</summary>
public sealed record ZaloSendRequest(string Phone, string TemplateId, IReadOnlyDictionary<string, string> Data, string TrackingId);

public sealed record ZaloSendResult(bool Success, string? MessageId, string? ErrorCode, string? ErrorMessage)
{
    public static ZaloSendResult Ok(string? messageId) => new(true, messageId, null, null);

    public static ZaloSendResult Fail(string code, string? message) => new(false, null, code, message);
}

/// <summary>Cổng gửi tin ra ngoài (ZNS thật, ghi log hoặc giả lập khi test).</summary>
public interface IZaloSender
{
    /// <summary>Không ném lỗi cho lỗi gửi thông thường; trả <see cref="ZaloSendResult.Fail"/>.</summary>
    Task<ZaloSendResult> SendAsync(ZaloSendRequest request, CancellationToken ct);
}

public sealed record ZaloStatusDto(bool Enabled, string Provider, bool SandboxMode, IReadOnlyList<string> MissingTemplates);

/// <summary>
/// <c>DailyReport</c>: cần <c>SessionDate</c>, mỗi học sinh đã điểm danh một tin riêng.
/// <c>Announcement</c>: cần <c>Content</c>, gửi cả lớp (mỗi số điện thoại một tin).
/// <c>StudentIds</c> trống = cả lớp.
/// </summary>
public sealed record ZaloBroadcastRequest(
    ZaloMessageKind Kind,
    DateOnly? SessionDate,
    string? Content,
    string? Deadline,
    string? Attention,
    IReadOnlyList<Guid>? StudentIds,
    bool Force = false);

public sealed record ZaloRecipientDto(
    Guid StudentId,
    string StudentName,
    string? ParentName,
    string? ParentPhone,
    /// <summary>NO_PHONE, INVALID_PHONE, NOT_MARKED, DUPLICATE_PHONE, NOT_SELECTED; null = sẽ được gửi.</summary>
    string? SkipReason,
    IReadOnlyDictionary<string, string>? Data);

public sealed record ZaloPreviewDto(
    ZaloMessageKind Kind, int SendableCount, bool AlreadySent, IReadOnlyList<ZaloRecipientDto> Recipients);

public sealed record ZaloBroadcastDto(
    Guid Id,
    ZaloMessageKind Kind,
    DateOnly? SessionDate,
    string? Content,
    DateTime CreatedAt,
    string? CreatedByName,
    int Total,
    int Pending,
    int Sent,
    int Failed,
    int Cancelled);

public sealed record ZaloBroadcastCreatedDto(ZaloBroadcastDto Broadcast, IReadOnlyList<ZaloRecipientDto> Skipped);

public sealed record ZaloMessageDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string? RecipientName,
    string Phone,
    ZaloMessageStatus Status,
    int Attempts,
    DateTime? SentAt,
    string? ErrorCode,
    string? ErrorMessage);

public sealed record ZaloBroadcastDetailDto(ZaloBroadcastDto Broadcast, IReadOnlyList<ZaloMessageDto> Messages);

public interface IZaloBroadcastService
{
    ZaloStatusDto GetStatus();

    Task<Result<ZaloPreviewDto>> PreviewAsync(Guid classroomId, ZaloBroadcastRequest request, CancellationToken ct);

    Task<Result<ZaloBroadcastCreatedDto>> CreateAsync(Guid classroomId, ZaloBroadcastRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<ZaloBroadcastDto>>> ListAsync(Guid classroomId, CancellationToken ct);

    Task<Result<ZaloBroadcastDetailDto>> GetAsync(Guid classroomId, Guid broadcastId, CancellationToken ct);

    /// <summary>Đưa các tin lỗi của đợt về hàng đợi để gửi lại.</summary>
    Task<Result<ZaloBroadcastDto>> RetryFailedAsync(Guid classroomId, Guid broadcastId, CancellationToken ct);

    /// <summary>Hủy các tin chưa gửi của đợt.</summary>
    Task<Result<ZaloBroadcastDto>> CancelAsync(Guid classroomId, Guid broadcastId, CancellationToken ct);
}

/// <summary>Lấy các tin đang chờ trong hàng đợi và gửi đi; job nền gọi định kỳ.</summary>
public interface IZaloDispatchService
{
    /// <summary>Trả số tin đã xử lý (thành công hoặc lỗi) trong lượt này.</summary>
    Task<int> DispatchPendingAsync(CancellationToken ct);
}
