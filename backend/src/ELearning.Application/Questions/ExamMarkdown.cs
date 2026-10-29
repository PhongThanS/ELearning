using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ELearning.Domain.Enums;
using ELearning.Domain.Grading;

namespace ELearning.Application.Questions;

/// <summary>Lỗi khi đọc / kiểm tra file đề dạng Markdown, kèm số dòng để sửa đúng chỗ.</summary>
public sealed record MarkdownImportIssue(int? Line, string? Field, string Code, string Message);

/// <summary>Phần đầu file: thông tin đề thi (để tạo đề nháp sau khi nhập câu hỏi).</summary>
public sealed record ExamMarkdownHeader(
    string? Title,
    string? Code,
    int? DurationMinutes,
    string? CategoryCode,
    string? Description,
    string? Instructions);

/// <summary>Một câu đọc được từ file. <see cref="Input"/> null khi câu sai cấu trúc tới mức không dựng được.</summary>
public sealed class ExamMarkdownQuestion
{
    public required int Line { get; init; }

    public required string Label { get; init; }

    public string? Part { get; init; }

    public QuestionInput? Input { get; set; }

    public List<MarkdownImportIssue> Issues { get; } = [];
}

public sealed record ExamMarkdownDocument(
    ExamMarkdownHeader Header,
    IReadOnlyList<ExamMarkdownQuestion> Questions,
    IReadOnlyList<MarkdownImportIssue> FileIssues);

/// <summary>
/// Đọc file đề "elearning-exam/1" (docs/02-nghiep-vu.md mục 1.5, D-29). Định dạng theo dòng: mỗi trường là một dòng
/// bắt đầu bằng <c>@khóa:</c> ở đầu dòng; các dòng sau (không bắt đầu bằng <c>@</c>) nối vào trường đang mở, nên nội
/// dung nhiều dòng (Markdown, LaTeX, bảng, ảnh) không cần escape. Dòng nội dung thật sự bắt đầu bằng <c>@</c> viết
/// <c>@@</c>. Không đoán: khóa lạ, thiếu trường, sai thứ tự lựa chọn đều là lỗi kèm số dòng.
/// </summary>
public static partial class ExamMarkdownParser
{
    public const string FormatId = "elearning-exam/1";
    public const int MaxQuestions = 500;
    public const int MaxLength = 2_000_000;

    private static readonly HashSet<string> HeaderKeys = ["format", "title", "code", "duration", "category", "description", "instructions"];
    private static readonly HashSet<string> DefaultKeys = ["type", "score", "difficulty", "tags"];
    private static readonly HashSet<string> QuestionKeys =
    [
        "type", "score", "difficulty", "tags", "code", "content", "option", "statement", "answer",
        "tolerance", "case_sensitive", "ignore_accent", "partial", "explanation",
    ];

    /// <summary>Khóa có giá trị nhiều dòng; các khóa còn lại chỉ một dòng.</summary>
    private static readonly HashSet<string> MultiLineKeys = ["content", "option", "statement", "explanation", "description", "instructions"];

    private const string OptionLetters = "ABCDEFGHIJ";
    private const string StatementLetters = "abcdef";

    public static ExamMarkdownDocument Parse(string text)
    {
        var fileIssues = new List<MarkdownImportIssue>();
        var directives = Tokenize(text, fileIssues);

        var header = new Dictionary<string, Directive>(StringComparer.Ordinal);
        var questions = new List<ExamMarkdownQuestion>();
        var defaults = new Dictionary<string, Directive>(StringComparer.Ordinal);
        string? part = null;
        QuestionBlock? current = null;
        var formatSeen = false;

        void Flush()
        {
            if (current is not null)
            {
                questions.Add(Build(current, defaults));
                current = null;
            }
        }

        foreach (var d in directives)
        {
            if (d.Key == "format")
            {
                formatSeen = true;
            }

            if (d.Key == "question")
            {
                Flush();
                current = new QuestionBlock(d.Line, string.IsNullOrWhiteSpace(d.Value) ? (questions.Count + 1).ToString(CultureInfo.InvariantCulture) : d.Value.Trim(), part);
                continue;
            }

            if (d.Key == "part")
            {
                Flush();
                part = string.IsNullOrWhiteSpace(d.Value) ? null : d.Value.Trim();
                defaults.Clear();
                continue;
            }

            if (d.Key == "end")
            {
                Flush();
                continue;
            }

            if (current is not null)
            {
                current.Add(d);
                continue;
            }

            if (part is not null && DefaultKeys.Contains(d.Key))
            {
                defaults[d.Key] = d;
            }
            else if (part is null && HeaderKeys.Contains(d.Key))
            {
                if (!header.TryAdd(d.Key, d))
                {
                    fileIssues.Add(new(d.Line, d.Key, "DUPLICATE_FIELD", $"Trường @{d.Key} bị khai báo hai lần."));
                }
            }
            else
            {
                fileIssues.Add(new(d.Line, d.Key, "MISPLACED_FIELD", $"Trường @{d.Key} không được đặt ở đây (chỉ dùng bên trong một @question)."));
            }
        }

        Flush();

        if (!formatSeen)
        {
            fileIssues.Insert(0, new(1, "format", "FORMAT_MISSING", $"Dòng đầu tiên của file phải là \"@format: {FormatId}\"."));
        }
        else if (header.TryGetValue("format", out var format) && !string.Equals(format.Value.Trim(), FormatId, StringComparison.OrdinalIgnoreCase))
        {
            fileIssues.Add(new(format.Line, "format", "FORMAT_UNSUPPORTED", $"Chỉ hỗ trợ định dạng \"{FormatId}\"."));
        }

        if (questions.Count == 0)
        {
            fileIssues.Add(new(null, null, "IMPORT_EMPTY", "File không có câu hỏi nào (mỗi câu bắt đầu bằng dòng \"@question: <số>\")."));
        }
        else if (questions.Count > MaxQuestions)
        {
            fileIssues.Add(new(null, null, "IMPORT_TOO_MANY", $"Tối đa {MaxQuestions} câu mỗi lần nhập."));
        }

        return new ExamMarkdownDocument(ReadHeader(header, fileIssues), questions, fileIssues);
    }

    // ------------------------------------------------------------------ Tách dòng thành các trường

    private sealed record Directive(int Line, string Key, string? Argument, string Value);

    private static List<Directive> Tokenize(string text, List<MarkdownImportIssue> issues)
    {
        var lines = text.TrimStart('﻿').ReplaceLineEndings("\n").Split('\n');
        BlankCodeFence(lines);
        var result = new List<Directive>();
        string? key = null;
        string? argument = null;
        var start = 0;
        StringBuilder? value = null;

        void Close()
        {
            if (key is null)
            {
                return;
            }

            var raw = value!.ToString();
            if (!MultiLineKeys.Contains(key))
            {
                var parts = raw.Split('\n');
                if (parts.Skip(1).Any(l => l.Trim().Length > 0))
                {
                    issues.Add(new(start, key, "SINGLE_LINE_FIELD", $"Trường @{key} chỉ được viết trên một dòng."));
                }

                raw = parts[0];
            }

            result.Add(new Directive(start, key, argument, TrimBlankLines(raw)));
            key = null;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNo = i + 1;
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                Append(line[1..]);
                continue;
            }

            if (line.StartsWith('@'))
            {
                if (line.Trim().TrimEnd(':').Equals("@end", StringComparison.OrdinalIgnoreCase))
                {
                    Close();
                    result.Add(new Directive(lineNo, "end", null, string.Empty));
                    continue;
                }

                var m = DirectiveLine().Match(line);
                if (!m.Success)
                {
                    issues.Add(new(lineNo, null, "INVALID_LINE", "Dòng bắt đầu bằng @ phải có dạng \"@khóa: giá trị\" (viết @@ nếu nội dung thật sự bắt đầu bằng @)."));
                    continue;
                }

                Close();
                var k = m.Groups["key"].Value.ToLowerInvariant();
                if (k is not ("question" or "part" or "end") && !HeaderKeys.Contains(k) && !QuestionKeys.Contains(k))
                {
                    issues.Add(new(lineNo, k, "UNKNOWN_FIELD", $"Không có trường @{k}. Xem lại tên trường trong hướng dẫn."));
                    continue;
                }

                key = k;
                argument = m.Groups["arg"].Success ? m.Groups["arg"].Value : null;
                start = lineNo;
                value = new StringBuilder(m.Groups["value"].Value.Trim());
                continue;
            }

            if (key is null)
            {
                if (line.Trim().Length > 0)
                {
                    issues.Add(new(lineNo, null, "TEXT_OUTSIDE_FIELD", "Có nội dung nằm ngoài mọi trường (phải nằm dưới @content, @option, @explanation…)."));
                }

                continue;
            }

            Append(line);
        }

        Close();
        return result;

        void Append(string line)
        {
            if (value is null)
            {
                return;
            }

            value.Append('\n').Append(line);
        }
    }

    /// <summary>
    /// AI thường trả file trong một khối ```markdown … ```; người dùng chép cả hai dòng rào thì bỏ qua chúng
    /// (thay bằng dòng trống để giữ đúng số dòng khi báo lỗi). Chỉ áp dụng khi rào bao toàn bộ file.
    /// </summary>
    private static void BlankCodeFence(string[] lines)
    {
        var first = Array.FindIndex(lines, l => l.Trim().Length > 0);
        var last = Array.FindLastIndex(lines, l => l.Trim().Length > 0);
        if (first >= 0 && last > first && lines[first].TrimStart().StartsWith("```", StringComparison.Ordinal) && lines[last].Trim() == "```")
        {
            lines[first] = string.Empty;
            lines[last] = string.Empty;
        }
    }

    private static string TrimBlankLines(string value)
    {
        var lines = value.Split('\n').ToList();
        while (lines.Count > 0 && lines[0].Trim().Length == 0)
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join('\n', lines.Select(l => l.TrimEnd()));
    }

    // ------------------------------------------------------------------ Phần đầu file

    private static ExamMarkdownHeader ReadHeader(Dictionary<string, Directive> header, List<MarkdownImportIssue> issues)
    {
        string? Get(string key) => header.TryGetValue(key, out var d) && d.Value.Length > 0 ? d.Value : null;

        int? duration = null;
        if (header.TryGetValue("duration", out var dur) && dur.Value.Length > 0)
        {
            if (int.TryParse(DigitsOnly().Replace(dur.Value, string.Empty), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes is > 0 and <= 600)
            {
                duration = minutes;
            }
            else
            {
                issues.Add(new(dur.Line, "duration", "INVALID_DURATION", "@duration là số phút từ 1 đến 600."));
            }
        }

        return new ExamMarkdownHeader(Get("title"), Get("code"), duration, Get("category"), Get("description"), Get("instructions"));
    }

    // ------------------------------------------------------------------ Dựng câu hỏi

    private sealed class QuestionBlock(int line, string label, string? part)
    {
        public int Line { get; } = line;

        public string Label { get; } = label;

        public string? Part { get; } = part;

        public List<Directive> Fields { get; } = [];

        public void Add(Directive d) => Fields.Add(d);
    }

    private static ExamMarkdownQuestion Build(QuestionBlock block, Dictionary<string, Directive> defaults)
    {
        var q = new ExamMarkdownQuestion { Line = block.Line, Label = block.Label, Part = block.Part };
        var issues = q.Issues;

        var single = new Dictionary<string, Directive>(StringComparer.Ordinal);
        var options = new List<Directive>();
        var statements = new List<Directive>();
        foreach (var f in block.Fields)
        {
            if (f.Key == "option")
            {
                options.Add(f);
            }
            else if (f.Key == "statement")
            {
                statements.Add(f);
            }
            else if (!single.TryAdd(f.Key, f))
            {
                issues.Add(new(f.Line, f.Key, "DUPLICATE_FIELD", $"Câu {block.Label}: trường @{f.Key} bị khai báo hai lần."));
            }
        }

        Directive? Field(string key) => single.TryGetValue(key, out var d) ? d : defaults.GetValueOrDefault(key);

        var typeField = Field("type");
        if (typeField is null || typeField.Value.Length == 0)
        {
            issues.Add(new(block.Line, "type", "TYPE_REQUIRED", $"Câu {block.Label}: thiếu @type."));
            return q;
        }

        var kind = typeField.Value.Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');
        if (kind is not ("SINGLE_CHOICE" or "MULTIPLE_CHOICE" or "TRUE_FALSE" or "NUMBER" or "TEXT" or "ESSAY"))
        {
            issues.Add(new(typeField.Line, "type", "TYPE_INVALID",
                $"Câu {block.Label}: @type phải là SINGLE_CHOICE, MULTIPLE_CHOICE, TRUE_FALSE, NUMBER, TEXT hoặc ESSAY."));
            return q;
        }

        var content = Field("content");
        if (content is null || content.Value.Length == 0)
        {
            issues.Add(new(content?.Line ?? block.Line, "content", "CONTENT_REQUIRED", $"Câu {block.Label}: thiếu @content."));
        }

        var score = ParseScore(Field("score"), block, issues);
        var difficulty = ParseDifficulty(Field("difficulty"), block, issues);
        var tags = Field("tags")?.Value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var answer = Field("answer");
        var explanation = Field("explanation")?.Value;

        if (kind is not ("SINGLE_CHOICE" or "MULTIPLE_CHOICE") && options.Count > 0)
        {
            issues.Add(new(options[0].Line, "option", "OPTION_NOT_ALLOWED", $"Câu {block.Label}: chỉ câu SINGLE_CHOICE / MULTIPLE_CHOICE có @option."));
        }

        if (kind != "TRUE_FALSE" && statements.Count > 0)
        {
            issues.Add(new(statements[0].Line, "statement", "STATEMENT_NOT_ALLOWED", $"Câu {block.Label}: chỉ câu TRUE_FALSE có @statement."));
        }

        var input = new QuestionInput
        {
            Code = Field("code")?.Value is { Length: > 0 } code ? code.Trim() : null,
            Content = content?.Value ?? string.Empty,
            ContentFormat = ContentFormat.Markdown,
            DefaultScore = score ?? 1,
            Difficulty = difficulty,
            Tags = tags is { Count: > 0 } ? tags : null,
            Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation,
        };

        switch (kind)
        {
            case "SINGLE_CHOICE" or "MULTIPLE_CHOICE":
                input = BuildChoice(input, kind, options, answer, Field("partial"), block, issues);
                break;
            case "TRUE_FALSE":
                input = BuildTrueFalse(input, statements, answer, block, issues);
                break;
            case "NUMBER":
                input = BuildNumber(input, answer, Field("tolerance"), block, issues);
                break;
            case "TEXT":
                input = BuildText(input, answer, Field("case_sensitive"), Field("ignore_accent"), block, issues);
                break;
            default:
                input = input with { QuestionType = QuestionType.Essay };
                if (answer is not null && answer.Value.Length > 0)
                {
                    issues.Add(new(answer.Line, "answer", "ANSWER_NOT_ALLOWED", $"Câu {block.Label}: câu ESSAY không có @answer (đáp án mẫu ghi ở @explanation)."));
                }

                break;
        }

        q.Input = input;
        return q;
    }

    private static QuestionInput BuildChoice(
        QuestionInput input, string kind, List<Directive> options, Directive? answer, Directive? partial, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        for (var i = 0; i < options.Count; i++)
        {
            var expected = i < OptionLetters.Length ? OptionLetters[i].ToString() : "?";
            if (!string.Equals(options[i].Argument, expected, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new(options[i].Line, "option", "OPTION_ORDER",
                    $"Câu {block.Label}: lựa chọn thứ {i + 1} phải là \"@option {expected}:\" (các lựa chọn liên tục từ A)."));
            }

            if (options[i].Value.Length == 0)
            {
                issues.Add(new(options[i].Line, "option", "OPTION_EMPTY", $"Câu {block.Label}: lựa chọn {expected} trống."));
            }
        }

        if (options.Count < 2)
        {
            issues.Add(new(block.Line, "option", "OPTIONS_REQUIRED", $"Câu {block.Label}: cần ít nhất 2 dòng @option."));
        }

        var correct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (RequireAnswer(answer, block, issues) is { } value)
        {
            foreach (var token in value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var letter = token.TrimEnd('.', ')').ToUpperInvariant();
                if (letter.Length != 1 || OptionLetters.IndexOf(letter[0], StringComparison.Ordinal) is var idx && (idx < 0 || idx >= options.Count))
                {
                    issues.Add(new(answer!.Line, "answer", "ANSWER_INVALID", $"Câu {block.Label}: đáp án \"{token}\" không phải một lựa chọn có trong câu."));
                    continue;
                }

                correct.Add(letter);
            }

            if (kind == "SINGLE_CHOICE" && correct.Count > 1)
            {
                issues.Add(new(answer!.Line, "answer", "ANSWER_INVALID", $"Câu {block.Label}: câu SINGLE_CHOICE chỉ có một đáp án."));
            }
        }

        return input with
        {
            QuestionType = kind == "SINGLE_CHOICE" ? QuestionType.SingleChoice : QuestionType.MultipleChoice,
            Options = options.Select((o, i) => new QuestionOptionInput(
                i < OptionLetters.Length ? OptionLetters[i].ToString() : null, o.Value, correct.Contains(OptionLetters[Math.Min(i, OptionLetters.Length - 1)].ToString()))).ToList(),
            PartialScoring = kind == "MULTIPLE_CHOICE" && ParseFlag(partial, block, issues),
        };
    }

    private static QuestionInput BuildTrueFalse(
        QuestionInput input, List<Directive> statements, Directive? answer, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            var expected = i < StatementLetters.Length ? StatementLetters[i].ToString() : "?";
            if (!string.Equals(statements[i].Argument, expected, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new(statements[i].Line, "statement", "STATEMENT_ORDER",
                    $"Câu {block.Label}: ý thứ {i + 1} phải là \"@statement {expected}:\" (các ý liên tục từ a)."));
            }

            if (statements[i].Value.Length == 0)
            {
                issues.Add(new(statements[i].Line, "statement", "STATEMENT_EMPTY", $"Câu {block.Label}: ý {expected} trống."));
            }
        }

        if (statements.Count is < 1 or > 6)
        {
            issues.Add(new(block.Line, "statement", "STATEMENTS_REQUIRED", $"Câu {block.Label}: câu TRUE_FALSE có từ 1 đến 6 dòng @statement."));
        }

        var truth = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (RequireAnswer(answer, block, issues) is { } value)
        {
            foreach (var token in value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var pair = token.Split(['=', ':'], 2, StringSplitOptions.TrimEntries);
                var flag = pair.Length == 2 ? ParseTruth(pair[1]) : null;
                if (pair.Length != 2 || pair[0].Length != 1 || flag is null)
                {
                    issues.Add(new(answer!.Line, "answer", "ANSWER_INVALID",
                        $"Câu {block.Label}: đáp án Đúng/Sai viết dạng \"a=Đ, b=S, c=Đ, d=S\" (gặp \"{token}\")."));
                    continue;
                }

                if (!truth.TryAdd(pair[0], flag.Value))
                {
                    issues.Add(new(answer!.Line, "answer", "ANSWER_INVALID", $"Câu {block.Label}: ý {pair[0]} có hai đáp án."));
                }
            }

            var codes = statements.Select((_, i) => i < StatementLetters.Length ? StatementLetters[i].ToString() : "?").ToList();
            var missing = codes.Where(c => !truth.ContainsKey(c)).ToList();
            var extra = truth.Keys.Where(k => !codes.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
            if (missing.Count > 0)
            {
                issues.Add(new(answer!.Line, "answer", "ANSWER_INCOMPLETE", $"Câu {block.Label}: thiếu đáp án cho ý {string.Join(", ", missing)}."));
            }

            if (extra.Count > 0)
            {
                issues.Add(new(answer!.Line, "answer", "ANSWER_INVALID", $"Câu {block.Label}: có đáp án cho ý không tồn tại: {string.Join(", ", extra)}."));
            }
        }

        return input with
        {
            QuestionType = QuestionType.TrueFalse,
            Options = statements.Select((s, i) =>
            {
                var code = i < StatementLetters.Length ? StatementLetters[i].ToString() : "z";
                return new QuestionOptionInput(code, s.Value, truth.TryGetValue(code, out var t) && t);
            }).ToList(),
        };
    }

    private static QuestionInput BuildNumber(QuestionInput input, Directive? answer, Directive? tolerance, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        decimal? number = null;
        if (RequireAnswer(answer, block, issues) is { } value)
        {
            if (NumericAnswerParser.TryParse(value, out var parsed))
            {
                number = parsed;
            }
            else
            {
                issues.Add(new(answer!.Line, "answer", "ANSWER_INVALID", $"Câu {block.Label}: đáp án \"{value}\" không phải là số (ví dụ 12, -3, 2,5)."));
            }
        }

        decimal? tol = null;
        if (tolerance is { Value.Length: > 0 })
        {
            if (NumericAnswerParser.TryParse(tolerance.Value, out var t) && t >= 0)
            {
                tol = t;
            }
            else
            {
                issues.Add(new(tolerance.Line, "tolerance", "TOLERANCE_INVALID", $"Câu {block.Label}: @tolerance là số không âm."));
            }
        }

        return input with
        {
            QuestionType = QuestionType.FillIn,
            AnswerDataType = AnswerDataType.Number,
            CorrectAnswerNumber = number,
            NumericTolerance = tol,
        };
    }

    private static QuestionInput BuildText(
        QuestionInput input, Directive? answer, Directive? caseSensitive, Directive? ignoreAccent, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        // "\|" (chép từ bảng Markdown) cũng là dấu phân cách
        var accepted = RequireAnswer(answer, block, issues)?
            .Replace("\\|", "|", StringComparison.Ordinal)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];
        return input with
        {
            QuestionType = QuestionType.FillIn,
            AnswerDataType = AnswerDataType.Text,
            AcceptedAnswers = accepted,
            CaseSensitive = ParseFlag(caseSensitive, block, issues),
            IgnoreAccent = ParseFlag(ignoreAccent, block, issues),
        };
    }

    private static string? RequireAnswer(Directive? answer, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        var value = answer?.Value.Trim();
        if (string.IsNullOrEmpty(value) || value == "?")
        {
            issues.Add(new(answer?.Line ?? block.Line, "answer", "ANSWER_REQUIRED", $"Câu {block.Label}: chưa có đáp án (@answer)."));
            return null;
        }

        return value;
    }

    private static decimal? ParseScore(Directive? field, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        if (field is null || field.Value.Length == 0)
        {
            issues.Add(new(block.Line, "score", "SCORE_REQUIRED", $"Câu {block.Label}: thiếu @score."));
            return null;
        }

        if (NumericAnswerParser.TryParse(field.Value, out var score) && score > 0)
        {
            return score;
        }

        issues.Add(new(field.Line, "score", "SCORE_INVALID", $"Câu {block.Label}: @score phải là số lớn hơn 0 (ví dụ 0,25)."));
        return null;
    }

    private static QuestionDifficulty? ParseDifficulty(Directive? field, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        if (field is null || field.Value.Length == 0)
        {
            return null;
        }

        switch (field.Value.Trim().ToUpperInvariant())
        {
            case "EASY" or "DỄ" or "NHẬN BIẾT":
                return QuestionDifficulty.Easy;
            case "MEDIUM" or "TRUNG BÌNH" or "THÔNG HIỂU":
                return QuestionDifficulty.Medium;
            case "HARD" or "KHÓ" or "VẬN DỤNG" or "VẬN DỤNG CAO":
                return QuestionDifficulty.Hard;
            default:
                issues.Add(new(field.Line, "difficulty", "DIFFICULTY_INVALID", $"Câu {block.Label}: @difficulty là EASY, MEDIUM hoặc HARD."));
                return null;
        }
    }

    private static bool ParseFlag(Directive? field, QuestionBlock block, List<MarkdownImportIssue> issues)
    {
        if (field is null || field.Value.Length == 0)
        {
            return false;
        }

        var flag = ParseTruth(field.Value);
        if (flag is null)
        {
            issues.Add(new(field.Line, field.Key, "FLAG_INVALID", $"Câu {block.Label}: @{field.Key} là yes hoặc no."));
        }

        return flag ?? false;
    }

    private static bool? ParseTruth(string value) => value.Trim().ToUpperInvariant() switch
    {
        "Đ" or "ĐÚNG" or "D" or "DUNG" or "T" or "TRUE" or "YES" or "Y" or "CÓ" or "1" => true,
        "S" or "SAI" or "F" or "FALSE" or "NO" or "N" or "KHÔNG" or "0" => false,
        _ => null,
    };

    // ------------------------------------------------------------------ Ảnh

    /// <summary>Tham chiếu ảnh trong Markdown: <c>![mô tả](đích)</c>.</summary>
    public static IEnumerable<(string Alt, string Target)> ImageRefs(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match m in ImageReference().Matches(text))
        {
            yield return (m.Groups["alt"].Value, m.Groups["target"].Value);
        }
    }

    [GeneratedRegex(@"^@(?<key>[A-Za-z_]+)(?:[ \t]+(?<arg>[A-Za-z0-9]+))?[ \t]*:(?<value>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveLine();

    [GeneratedRegex(@"[^0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsOnly();

    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex ImageReference();
}
