using ELearning.Domain.Common;

namespace ELearning.Domain.Settings;

/// <summary>
/// Cấu hình động toàn hệ thống (bật/tắt tự đăng ký, cờ vận hành...).
/// Lưu trong bảng SystemSettings, có thể điều chỉnh qua giao diện quản trị mà không cần restart máy chủ.
/// </summary>
public sealed class SystemSetting
{
    private SystemSetting()
    {
    }

    public SystemSetting(string key, string value, string? description, DateTime updatedAt, Guid? updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
        Value = value;
        Description = description;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public string Key { get; private set; } = null!;

    public string Value { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public void UpdateValue(string value, DateTime now, Guid? updatedBy)
    {
        Value = value;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }
}
