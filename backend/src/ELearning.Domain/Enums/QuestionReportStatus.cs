namespace ELearning.Domain.Enums;

/// <summary>Trạng thái một báo lỗi câu hỏi (D-46).</summary>
public enum QuestionReportStatus
{
    /// <summary>Chờ người có quyền kiểm tra.</summary>
    Open = 1,

    /// <summary>Đã kiểm tra: câu lỗi thật và đã sửa / hủy câu.</summary>
    Resolved = 2,

    /// <summary>Đã kiểm tra: câu không lỗi.</summary>
    Rejected = 3,
}
