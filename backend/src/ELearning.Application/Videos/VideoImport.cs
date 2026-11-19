using ELearning.Application.Questions;

namespace ELearning.Application.Videos;

/// <summary>
/// Tên cột của file import video bài giảng (D-44). Cột nhận theo tiêu đề (không phân biệt hoa thường,
/// bỏ khoảng trắng thừa) nên thứ tự cột không quan trọng.
/// </summary>
public static class VideoImportColumns
{
    public const string Title = "Tiêu đề";
    public const string VideoUrl = "Link video";
    public const string DocumentUrl = "Link tài liệu PDF";
    public const string HomeworkUrl = "Link bài tập về nhà";
    public const string Category = "Mã chuyên đề";
    public const string Classes = "Lớp";
    public const string PracticeExam = "Mã đề luyện";

    /// <summary>Số video tối đa mỗi lần import (tiêu đề trống thì hỏi YouTube từng video).</summary>
    public const int MaxRows = 200;

    public static IReadOnlyList<string> TemplateHeaders { get; } =
        [VideoUrl, Title, DocumentUrl, HomeworkUrl, Category, Classes, PracticeExam];

    public static string Normalize(string header) => QuestionImportColumns.Normalize(header);
}

/// <summary>Đọc / tạo file Excel import video (Infrastructure, ClosedXML).</summary>
public interface IVideoImportWorkbook
{
    /// <summary>Đọc sheet dữ liệu; ném <see cref="InvalidDataException"/> nếu không phải file .xlsx hợp lệ.</summary>
    QuestionImportSheet Read(Stream stream, int maxRows);

    byte[] CreateTemplate(
        IReadOnlyList<(string Code, string Name)> classes,
        IReadOnlyList<(string Code, string Name)> categories,
        IReadOnlyList<(string Code, string Name)> practiceExams);
}

public sealed record VideoImportRowResult(
    int RowNumber,
    string? Title,
    string? VideoUrl,
    string? CategoryName,
    IReadOnlyList<string> Classes,
    string? PracticeExamName,
    IReadOnlyList<QuestionImportIssue> Issues);

/// <summary>Tất cả hoặc không: một dòng lỗi thì không tạo video nào (<see cref="Imported"/> = false).</summary>
public sealed record VideoImportResult(
    bool DryRun,
    bool Imported,
    int TotalRows,
    int ValidRows,
    int ImportedCount,
    IReadOnlyList<VideoImportRowResult> Rows);
