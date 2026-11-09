namespace ELearning.Domain.Enums;

/// <summary>Loại đề (D-33).</summary>
public enum ExamKind
{
    /// <summary>Đề tính điểm: giữ nguyên quy tắc lượt thi, thời lượng, lịch thi.</summary>
    Graded = 1,

    /// <summary>Đề luyện: làm lại không giới hạn, không tính giờ (vẫn tôn trọng lịch mở / đóng đề).</summary>
    Practice = 2,
}
