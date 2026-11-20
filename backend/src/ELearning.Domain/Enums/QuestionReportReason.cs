namespace ELearning.Domain.Enums;

/// <summary>Lý do học sinh báo một câu hỏi bị lỗi (D-46).</summary>
public enum QuestionReportReason
{
    /// <summary>Đáp án đúng của câu bị sai.</summary>
    WrongAnswer = 1,

    /// <summary>Nội dung đề sai / thiếu dữ kiện / không có đáp án đúng.</summary>
    WrongContent = 2,

    /// <summary>Sai chính tả, lỗi định dạng, công thức hiển thị lỗi.</summary>
    Typo = 3,

    /// <summary>Thiếu hình / hình không hiện.</summary>
    MissingMedia = 4,

    Other = 5,
}
