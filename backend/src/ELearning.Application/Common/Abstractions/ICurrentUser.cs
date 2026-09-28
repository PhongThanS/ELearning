namespace ELearning.Application.Common.Abstractions;

/// <summary>Thông tin request hiện tại (người dùng, IP, user agent, trace id).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? TraceId { get; }

    /// <summary>UserId của người đã đăng nhập; ném lỗi nếu gọi trong request ẩn danh.</summary>
    Guid RequiredUserId => UserId ?? throw new InvalidOperationException("Request chưa được xác thực.");
}
