using ELearning.Domain.Common;

namespace ELearning.Domain.Attendance;

/// <summary>Liên hệ phụ huynh của học sinh (D-31): một dòng mỗi học sinh, giáo viên cập nhật từ trang điểm danh.</summary>
public sealed class StudentContact
{
    public const int MaxParentNameLength = 200;
    public const int MaxPhoneLength = 20;

    private StudentContact()
    {
    }

    public StudentContact(Guid userId)
    {
        UserId = userId;
    }

    public Guid UserId { get; private set; }

    public string? ParentName { get; private set; }

    /// <summary>Số điện thoại phụ huynh (dùng cho nút gọi / Zalo).</summary>
    public string? ParentPhone { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Update(string? parentName, string? parentPhone, Guid updatedBy, DateTime now)
    {
        ParentName = AttendanceSession.Clean(parentName, MaxParentNameLength);
        var phone = AttendanceSession.Clean(parentPhone, MaxPhoneLength);
        if (phone is not null && !IsValidPhone(phone))
        {
            throw new DomainException(DomainErrorCodes.InvalidAttendance, "Số điện thoại chỉ gồm chữ số, dấu +, khoảng trắng, dấu chấm, gạch ngang.");
        }

        ParentPhone = phone;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public static bool IsValidPhone(string phone) =>
        phone.Length <= MaxPhoneLength && phone.Count(char.IsAsciiDigit) >= 8
        && phone.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '.' or '-');
}
