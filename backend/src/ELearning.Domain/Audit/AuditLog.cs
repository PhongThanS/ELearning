namespace ELearning.Domain.Audit;

/// <summary>Nhật ký thao tác quan trọng (docs/07-bao-mat.md mục 9). Không có FK tới Users.</summary>
public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        string action,
        Guid? userId,
        string? entityName,
        Guid? entityId,
        string? oldValue,
        string? newValue,
        string? reason,
        string? ipAddress,
        string? userAgent,
        string? traceId,
        DateTime createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        Action = action;
        UserId = userId;
        EntityName = entityName;
        EntityId = entityId;
        OldValue = oldValue;
        NewValue = newValue;
        Reason = reason;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        TraceId = traceId;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = null!;

    public string? EntityName { get; private set; }

    public Guid? EntityId { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    public string? Reason { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? TraceId { get; private set; }

    public DateTime CreatedAt { get; private set; }
}
