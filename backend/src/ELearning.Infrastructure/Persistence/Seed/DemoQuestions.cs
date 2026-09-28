using ELearning.Domain.Enums;
using ELearning.Domain.Questions;

namespace ELearning.Infrastructure.Persistence.Seed;

/// <summary>Dữ liệu demo cho Development: danh mục và 10 câu hỏi "C# Basic" đủ 4 loại (docs/09-van-hanh.md mục 3).</summary>
internal static class DemoQuestions
{
    public const string CSharpCategoryCode = "CSHARP";

    public static readonly (string Code, string Name)[] Categories =
    [
        (CSharpCategoryCode, "C#"),
        ("SQLSERVER", "SQL Server"),
        ("ASPNET", "ASP.NET"),
        ("REACTJS", "ReactJS"),
        ("JAVASCRIPT", "JavaScript"),
    ];

    public static IEnumerable<(string Code, QuestionData Data)> CSharpBasic(Guid categoryId)
    {
        yield return ("CS-001", Single(
            categoryId,
            "Kiểu dữ liệu nào dùng để lưu số thực có độ chính xác cao, phù hợp cho tiền tệ?",
            ("A", "float", false), ("B", "double", false), ("C", "decimal", true), ("D", "int", false)));

        yield return ("CS-002", Single(
            categoryId,
            "Đoạn code sau in ra gì?\n\n```csharp\nvar list = new List<int> { 1, 2, 3 };\nConsole.WriteLine(list.Count);\n```",
            ("A", "2", false), ("B", "3", true), ("C", "4", false), ("D", "Lỗi biên dịch", false),
            markdown: true));

        yield return ("CS-003", Multiple(
            categoryId,
            "Những từ khóa nào là **access modifier** trong C#?",
            ("A", "public", true), ("B", "static", false), ("C", "internal", true), ("D", "protected", true)));

        yield return ("CS-004", TrueFalse(categoryId, "`string` trong C# là kiểu tham chiếu (reference type).", correct: true));

        yield return ("CS-005", TrueFalse(categoryId, "Một class trong C# có thể kế thừa trực tiếp từ nhiều class.", correct: false));

        yield return ("CS-006", FillText(
            categoryId,
            "Từ khóa nào dùng để khai báo một phương thức bất đồng bộ?",
            ["async"]));

        yield return ("CS-007", FillNumber(
            categoryId,
            "Kết quả của biểu thức `7 / 2` với hai số kiểu `int` là bao nhiêu?",
            3m,
            0m));

        yield return ("CS-008", Single(
            categoryId,
            "Interface nào cho phép duyệt một tập hợp bằng `foreach`?",
            ("A", "IDisposable", false), ("B", "IEnumerable", true), ("C", "IComparable", false), ("D", "ICloneable", false)));

        yield return ("CS-009", Multiple(
            categoryId,
            "Những phương thức LINQ nào thực thi ngay (không trì hoãn)?",
            ("A", "Where", false), ("B", "ToList", true), ("C", "Count", true), ("D", "Select", false)));

        yield return ("CS-010", FillText(
            categoryId,
            "Kiểu giá trị có thể nhận `null` được khai báo bằng ký hiệu gì sau tên kiểu (ví dụ `int_`)?",
            ["?", "dấu hỏi", "dấu chấm hỏi"],
            ignoreAccent: true));
    }

    private static QuestionData Single(Guid categoryId, string content, params (string Code, string Text, bool Correct)[] options) =>
        Choice(categoryId, QuestionType.SingleChoice, content, false, options);

    private static QuestionData Single(
        Guid categoryId, string content, (string, string, bool) a, (string, string, bool) b, (string, string, bool) c,
        (string, string, bool) d, bool markdown) =>
        Choice(categoryId, QuestionType.SingleChoice, content, markdown, [a, b, c, d]);

    private static QuestionData Multiple(Guid categoryId, string content, params (string Code, string Text, bool Correct)[] options) =>
        Choice(categoryId, QuestionType.MultipleChoice, content, true, options);

    private static QuestionData Choice(
        Guid categoryId, QuestionType type, string content, bool markdown, (string Code, string Text, bool Correct)[] options) =>
        new(
            categoryId,
            content,
            markdown ? ContentFormat.Markdown : ContentFormat.Plain,
            type,
            null,
            null,
            null,
            false,
            false,
            null,
            1m,
            options.Select(o => new OptionData(o.Code, o.Text, o.Correct)).ToList(),
            []);

    private static QuestionData TrueFalse(Guid categoryId, string content, bool correct) =>
        new(
            categoryId,
            content,
            ContentFormat.Markdown,
            QuestionType.TrueFalse,
            null,
            null,
            null,
            false,
            false,
            null,
            1m,
            [new OptionData(Question.TrueCode, "Đúng", correct), new OptionData(Question.FalseCode, "Sai", !correct)],
            []);

    private static QuestionData FillText(Guid categoryId, string content, string[] accepted, bool ignoreAccent = false) =>
        new(
            categoryId,
            content,
            ContentFormat.Markdown,
            QuestionType.FillIn,
            AnswerDataType.Text,
            null,
            null,
            false,
            ignoreAccent,
            null,
            1m,
            [],
            accepted);

    private static QuestionData FillNumber(Guid categoryId, string content, decimal answer, decimal tolerance) =>
        new(
            categoryId,
            content,
            ContentFormat.Markdown,
            QuestionType.FillIn,
            AnswerDataType.Number,
            answer,
            tolerance,
            false,
            false,
            "Chia hai số nguyên trong C# cho kết quả nguyên (bỏ phần thập phân).",
            1m,
            [],
            []);
}
