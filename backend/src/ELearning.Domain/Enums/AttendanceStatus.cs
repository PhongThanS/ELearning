namespace ELearning.Domain.Enums;

/// <summary>Trạng thái điểm danh của học sinh trong một buổi học (D-31). Chưa điểm danh = NULL.</summary>
public enum AttendanceStatus
{
    Present = 1,
    Late = 2,
    Excused = 3,
    Unexcused = 4,

    /// <summary>Học trực tuyến (không đến lớp nhưng có tham gia buổi học) — tính là có mặt khi tính chuyên cần.</summary>
    Online = 5,
}
