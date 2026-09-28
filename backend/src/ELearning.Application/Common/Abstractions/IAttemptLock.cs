namespace ELearning.Application.Common.Abstractions;

/// <summary>
/// Thao tác cần SQL riêng của SQL Server (D-21, D-23). Hiện thực ở Infrastructure.
/// </summary>
public interface IAttemptLock
{
    /// <summary>
    /// Khóa dòng ExamAttempts bằng UPDLOCK, ROWLOCK trong transaction hiện tại, để lưu đáp án và nộp bài
    /// không chạy chen nhau (docs/10-bay-ky-thuat.md mục 6). Phải gọi bên trong transaction.
    /// </summary>
    Task LockAsync(Guid attemptId, CancellationToken ct);

    /// <summary>Lỗi vi phạm unique index (2601/2627), ví dụ khi hai request start chạy song song.</summary>
    bool IsUniqueViolation(Exception exception);
}
