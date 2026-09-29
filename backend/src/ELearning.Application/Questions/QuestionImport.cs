using System.Globalization;
using System.Text;
using ELearning.Domain.Enums;
using ELearning.Domain.Grading;
using ELearning.Domain.Questions;

namespace ELearning.Application.Questions;

/// <summary>
/// Tên cột của file import câu hỏi (docs/02-nghiep-vu.md mục 1.4). Cột được nhận theo tiêu đề
/// (không phân biệt hoa thường, bỏ khoảng trắng thừa) nên thứ tự cột không quan trọng.
/// </summary>
public static class QuestionImportColumns
{
    public const string Code = "Mã";
    public const string Category = "Danh mục";
    public const string Type = "Loại";
    public const string Format = "Định dạng";
    public const string Content = "Nội dung";
    public const string Score = "Điểm";
    public const string OptionPrefix = "Lựa chọn ";
    public const string Correct = "Đáp án đúng";
    public const string AnswerDataType = "Kiểu đáp án";
    public const string AcceptedAnswers = "Đáp án chấp nhận";
    public const string CorrectNumber = "Đáp án số";
    public const string Tolerance = "Sai số";
    public const string CaseSensitive = "Phân biệt hoa thường";
    public const string IgnoreAccent = "Bỏ qua dấu";
    public const string Explanation = "Giải thích";

    /// <summary>Số câu tối đa mỗi lần import.</summary>
    public const int MaxRows = 1000;

    public static readonly string[] OptionCodes = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J"];

    /// <summary>Thứ tự cột trong file mẫu (6 lựa chọn A–F; file tự làm có thể thêm tới J).</summary>
    public static IReadOnlyList<string> TemplateHeaders { get; } =
    [
        Code, Category, Type, Format, Content, Score,
        .. OptionCodes.Take(6).Select(c => OptionPrefix + c),
        Correct, AnswerDataType, AcceptedAnswers, CorrectNumber, Tolerance, CaseSensitive, IgnoreAccent, Explanation,
    ];

    public static string Normalize(string header) =>
        string.Join(' ', header.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
}

/// <summary>Một dòng dữ liệu đọc từ file: giá trị dạng chuỗi theo tên cột đã chuẩn hóa.</summary>
public sealed record QuestionImportRawRow(int RowNumber, IReadOnlyDictionary<string, string> Cells);

public sealed record QuestionImportSheet(IReadOnlyList<QuestionImportRawRow> Rows, IReadOnlyList<string> Headers);

/// <summary>Đọc / tạo file Excel (Infrastructure, ClosedXML).</summary>
public interface IQuestionImportWorkbook
{
    /// <summary>Đọc sheet đầu tiên; ném <see cref="InvalidDataException"/> nếu không phải file .xlsx hợp lệ.</summary>
    QuestionImportSheet Read(Stream stream, int maxRows);

    byte[] CreateTemplate(IReadOnlyList<(string Code, string Name)> categories);
}

public sealed record QuestionImportIssue(string? Field, string Code, string Message);

public sealed record QuestionImportRowResult(
    int RowNumber,
    string? Code,
    QuestionType? QuestionType,
    string ContentPreview,
    IReadOnlyList<QuestionImportIssue> Issues);

/// <summary>
/// Kết quả import. Import là tất cả hoặc không: chỉ cần một dòng lỗi thì không câu nào được tạo
/// (<see cref="Imported"/> = false) và <see cref="Rows"/> cho biết lỗi từng dòng.
/// </summary>
public sealed record QuestionImportResult(
    bool DryRun,
    bool Imported,
    int TotalRows,
    int ValidRows,
    int ImportedCount,
    IReadOnlyList<QuestionImportRowResult> Rows);

/// <summary>Chuyển một dòng chuỗi thành <see cref="QuestionInput"/>; lỗi định dạng ghi vào <paramref name="issues"/>.</summary>
internal static class QuestionImportMapper
{
    public static (QuestionInput? Input, string? CategoryCode) Map(QuestionImportRawRow row, List<QuestionImportIssue> issues)
    {
        string? Get(string column) =>
            row.Cells.TryGetValue(QuestionImportColumns.Normalize(column), out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        var type = ParseType(Get(QuestionImportColumns.Type));
        if (type is null)
        {
            issues.Add(new(QuestionImportColumns.Type, "QUESTION_TYPE_INVALID",
                "Loại câu hỏi phải là SINGLE_CHOICE, MULTIPLE_CHOICE, TRUE_FALSE hoặc FILL_IN (hoặc Chọn một, Chọn nhiều, Đúng / Sai, Điền đáp án)."));
            return (null, Get(QuestionImportColumns.Category));
        }

        var format = Get(QuestionImportColumns.Format)?.ToUpperInvariant() switch
        {
            null or "MARKDOWN" => ContentFormat.Markdown,
            "PLAIN" or "VĂN BẢN" or "TEXT" => ContentFormat.Plain,
            _ => (ContentFormat?)null,
        };
        if (format is null)
        {
            issues.Add(new(QuestionImportColumns.Format, "CONTENT_FORMAT_INVALID", "Định dạng phải là MARKDOWN hoặc PLAIN."));
        }

        var score = 1m;
        if (Get(QuestionImportColumns.Score) is { } scoreText && !NumericAnswerParser.TryParse(scoreText, out score))
        {
            issues.Add(new(QuestionImportColumns.Score, "SCORE_INVALID", "Điểm phải là số (ví dụ 1 hoặc 0,5)."));
        }

        var input = new QuestionInput
        {
            Code = Get(QuestionImportColumns.Code),
            Content = Get(QuestionImportColumns.Content) ?? string.Empty,
            ContentFormat = format ?? ContentFormat.Markdown,
            QuestionType = type.Value,
            DefaultScore = score,
            Explanation = Get(QuestionImportColumns.Explanation),
            CaseSensitive = ParseBool(Get(QuestionImportColumns.CaseSensitive), QuestionImportColumns.CaseSensitive, issues),
            IgnoreAccent = ParseBool(Get(QuestionImportColumns.IgnoreAccent), QuestionImportColumns.IgnoreAccent, issues),
        };

        var correct = Get(QuestionImportColumns.Correct);
        input = type switch
        {
            QuestionType.SingleChoice or QuestionType.MultipleChoice => input with { Options = ChoiceOptions(Get, correct, issues) },
            QuestionType.TrueFalse => input with { Options = TrueFalseOptions(correct, issues) },
            _ => FillIn(input, Get, issues),
        };
        return (input, Get(QuestionImportColumns.Category));
    }

    private static QuestionType? ParseType(string? text) =>
        Compact(text) switch
        {
            "SINGLE_CHOICE" or "SINGLECHOICE" or "CHỌNMỘT" => QuestionType.SingleChoice,
            "MULTIPLE_CHOICE" or "MULTIPLECHOICE" or "CHỌNNHIỀU" => QuestionType.MultipleChoice,
            "TRUE_FALSE" or "TRUEFALSE" or "ĐÚNG/SAI" => QuestionType.TrueFalse,
            "FILL_IN" or "FILLIN" or "ĐIỀNĐÁPÁN" or "ĐIỀN" => QuestionType.FillIn,
            _ => null,
        };

    private static List<QuestionOptionInput> ChoiceOptions(Func<string, string?> get, string? correct, List<QuestionImportIssue> issues)
    {
        var correctCodes = (correct ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        if (correctCodes.Count == 0)
        {
            issues.Add(new(QuestionImportColumns.Correct, "CORRECT_REQUIRED", "Vui lòng ghi đáp án đúng (ví dụ B hoặc A, C)."));
        }

        var options = new List<QuestionOptionInput>();
        foreach (var code in QuestionImportColumns.OptionCodes)
        {
            if (get(QuestionImportColumns.OptionPrefix + code) is { } content)
            {
                options.Add(new QuestionOptionInput(code, content, correctCodes.Contains(code)));
            }
        }

        // Lựa chọn phải liên tục từ A (không bỏ trống ở giữa) để mã lựa chọn khớp với cột
        for (var i = 0; i < options.Count; i++)
        {
            if (options[i].OptionCode != QuestionImportColumns.OptionCodes[i])
            {
                issues.Add(new(QuestionImportColumns.OptionPrefix + QuestionImportColumns.OptionCodes[i], "OPTION_GAP",
                    $"Lựa chọn {QuestionImportColumns.OptionCodes[i]} bị bỏ trống trong khi có lựa chọn sau nó."));
                break;
            }
        }

        var unknown = correctCodes.Where(c => options.All(o => o.OptionCode != c)).ToList();
        if (unknown.Count > 0)
        {
            issues.Add(new(QuestionImportColumns.Correct, "CORRECT_OPTION_NOT_FOUND",
                $"Đáp án đúng {string.Join(", ", unknown)} không có nội dung lựa chọn tương ứng."));
        }

        return options;
    }

    private static List<QuestionOptionInput> TrueFalseOptions(string? correct, List<QuestionImportIssue> issues)
    {
        bool? isTrue = Compact(correct) switch
        {
            "TRUE" or "ĐÚNG" or "Đ" or "T" or "1" => true,
            "FALSE" or "SAI" or "S" or "F" or "0" => false,
            _ => null,
        };
        if (isTrue is null)
        {
            issues.Add(new(QuestionImportColumns.Correct, "TRUE_FALSE_INVALID", "Đáp án câu Đúng/Sai phải là Đúng hoặc Sai (TRUE / FALSE)."));
        }

        return
        [
            new QuestionOptionInput(Question.TrueCode, "Đúng", isTrue == true),
            new QuestionOptionInput(Question.FalseCode, "Sai", isTrue == false),
        ];
    }

    private static QuestionInput FillIn(QuestionInput input, Func<string, string?> get, List<QuestionImportIssue> issues)
    {
        var numberText = get(QuestionImportColumns.CorrectNumber);
        AnswerDataType? dataType = Compact(get(QuestionImportColumns.AnswerDataType)) switch
        {
            null => numberText is null ? AnswerDataType.Text : AnswerDataType.Number,
            "TEXT" or "CHỮ" => AnswerDataType.Text,
            "NUMBER" or "SỐ" => AnswerDataType.Number,
            _ => null,
        };
        if (dataType is null)
        {
            issues.Add(new(QuestionImportColumns.AnswerDataType, "ANSWER_DATA_TYPE_INVALID", "Kiểu đáp án phải là TEXT (Chữ) hoặc NUMBER (Số)."));
            return input;
        }

        if (dataType == AnswerDataType.Text)
        {
            var accepted = (get(QuestionImportColumns.AcceptedAnswers) ?? string.Empty)
                .Split(['|', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            return input with { AnswerDataType = AnswerDataType.Text, AcceptedAnswers = accepted };
        }

        decimal? number = null;
        if (numberText is not null)
        {
            if (NumericAnswerParser.TryParse(numberText, out var n))
            {
                number = n;
            }
            else
            {
                issues.Add(new(QuestionImportColumns.CorrectNumber, "CORRECT_NUMBER_INVALID", "Đáp án số không hợp lệ (ví dụ 3,5 hoặc 3.5)."));
            }
        }

        var tolerance = 0m;
        if (get(QuestionImportColumns.Tolerance) is { } toleranceText && !NumericAnswerParser.TryParse(toleranceText, out tolerance))
        {
            issues.Add(new(QuestionImportColumns.Tolerance, "TOLERANCE_INVALID", "Sai số phải là số."));
        }

        return input with { AnswerDataType = AnswerDataType.Number, CorrectAnswerNumber = number, NumericTolerance = tolerance };
    }

    private static bool ParseBool(string? text, string column, List<QuestionImportIssue> issues)
    {
        switch (Compact(text))
        {
            case null or "KHÔNG" or "K" or "N" or "NO" or "FALSE" or "0":
                return false;
            case "CÓ" or "C" or "X" or "Y" or "YES" or "TRUE" or "1":
                return true;
            default:
                issues.Add(new(column, "BOOLEAN_INVALID", $"Cột \"{column}\" chỉ nhận Có hoặc Không."));
                return false;
        }
    }

    /// <summary>Viết hoa, NFC, bỏ khoảng trắng: "Đúng / Sai" → "ĐÚNG/SAI".</summary>
    private static string? Compact(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : string.Concat(text.Normalize(NormalizationForm.FormC).Where(c => !char.IsWhiteSpace(c))).ToUpper(CultureInfo.InvariantCulture);
}
