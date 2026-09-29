using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Common;
using ELearning.Domain.Questions;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Questions;

public interface IQuestionImportService
{
    Task<Result<QuestionImportResult>> ImportAsync(Stream file, bool dryRun, CancellationToken ct);

    Task<byte[]> CreateTemplateAsync(CancellationToken ct);
}

/// <summary>
/// Import câu hỏi từ Excel (docs/02-nghiep-vu.md mục 1.4). Mỗi dòng đi qua đúng validator và domain của
/// chức năng tạo câu hỏi. Tất cả hoặc không: có dòng lỗi thì không tạo câu nào.
/// </summary>
internal sealed class QuestionImportService(
    IAppDbContext db,
    IQuestionImportWorkbook workbook,
    ICodeGenerator codeGenerator,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<QuestionInput> validator) : IQuestionImportService
{
    /// <summary>Tên trường của validator → tên cột trong file, để thông báo lỗi chỉ đúng chỗ cần sửa.</summary>
    private static readonly (string Field, string Column)[] FieldColumns =
    [
        ("code", QuestionImportColumns.Code),
        ("content", QuestionImportColumns.Content),
        ("defaultScore", QuestionImportColumns.Score),
        ("options", QuestionImportColumns.Correct),
        ("acceptedAnswers", QuestionImportColumns.AcceptedAnswers),
        ("correctAnswerNumber", QuestionImportColumns.CorrectNumber),
        ("numericTolerance", QuestionImportColumns.Tolerance),
        ("answerDataType", QuestionImportColumns.AnswerDataType),
        ("explanation", QuestionImportColumns.Explanation),
    ];

    public async Task<Result<QuestionImportResult>> ImportAsync(Stream file, bool dryRun, CancellationToken ct)
    {
        QuestionImportSheet sheet;
        try
        {
            sheet = workbook.Read(file, QuestionImportColumns.MaxRows);
        }
        catch (InvalidDataException ex)
        {
            return Error.Validation("IMPORT_FILE_INVALID", ex.Message, "file");
        }

        var missing = new[] { QuestionImportColumns.Type, QuestionImportColumns.Content }
            .Where(h => !sheet.Headers.Contains(QuestionImportColumns.Normalize(h)))
            .ToList();
        if (missing.Count > 0)
        {
            return Error.Validation("IMPORT_COLUMNS_MISSING", $"File thiếu cột: {string.Join(", ", missing)}. Hãy dùng file mẫu.", "file");
        }

        if (sheet.Rows.Count == 0)
        {
            return Error.Validation("IMPORT_EMPTY", "File không có dòng câu hỏi nào.", "file");
        }

        var rows = sheet.Rows.Select(raw =>
        {
            var row = new ImportRow(raw);
            (row.Input, row.CategoryCode) = QuestionImportMapper.Map(raw, row.Issues);
            return row;
        }).ToList();

        await ResolveCategoriesAsync(rows, ct);
        await CheckCodesAsync(rows, ct);
        foreach (var row in rows.Where(r => r.Input is not null))
        {
            await ValidateAsync(row, ct);
        }

        var results = rows.Select(r => new QuestionImportRowResult(
                r.Raw.RowNumber, r.Input?.Code, r.Input?.QuestionType, Preview(r.Input?.Content), r.Issues))
            .ToList();
        var validRows = rows.Count(r => r.Issues.Count == 0);
        if (dryRun || validRows != rows.Count)
        {
            return new QuestionImportResult(dryRun, Imported: false, rows.Count, validRows, 0, results);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var created = await db.InTransactionAsync(
            async () =>
            {
                var questions = new List<Question>();
                foreach (var input in rows.Select(r => r.Input!))
                {
                    var code = string.IsNullOrWhiteSpace(input.Code) ? await codeGenerator.NextQuestionCodeAsync(ct) : input.Code.Trim();
                    var question = Question.Create(code, QuestionService.ToData(input), currentUser.RequiredUserId, now);
                    db.Questions.Add(question);
                    questions.Add(question);
                }

                audit.Write(
                    AuditActions.QuestionsImported,
                    nameof(Question),
                    newValue: new { Count = questions.Count, Codes = questions.Select(q => q.Code).ToList() });
                await db.SaveChangesAsync(ct);
                return questions;
            },
            ct);

        var imported = results.Select((r, i) => r with { Code = created[i].Code }).ToList();
        return new QuestionImportResult(DryRun: false, Imported: true, rows.Count, validRows, created.Count, imported);
    }

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct)
    {
        var categories = await db.QuestionCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Take(500)
            .Select(c => new { c.Code, c.Name })
            .ToListAsync(ct);
        return workbook.CreateTemplate(categories.Select(c => (c.Code, c.Name)).ToList());
    }

    private async Task ValidateAsync(ImportRow row, CancellationToken ct)
    {
        foreach (var error in await validator.ValidateToErrorsAsync(row.Input!, ct))
        {
            row.Issues.Add(new QuestionImportIssue(ColumnFor(error.Field), error.Code, error.Message));
        }

        if (row.Issues.Count > 0)
        {
            return;
        }

        // Lớp bảo vệ cuối: domain tự kiểm tra bất biến, không dựa hoàn toàn vào validator
        try
        {
            Question.Create("IMPORT-CHECK", QuestionService.ToData(row.Input!), Guid.Empty, DateTime.UnixEpoch);
        }
        catch (DomainException ex)
        {
            row.Issues.Add(new QuestionImportIssue(null, ex.Code, ex.Message));
        }
    }

    /// <summary>Cột "Danh mục" ghi mã danh mục; chỉ nhận danh mục đang hoạt động.</summary>
    private async Task ResolveCategoriesAsync(List<ImportRow> rows, CancellationToken ct)
    {
        var withCategory = rows.Where(r => r.Input is not null && r.CategoryCode is not null).ToList();
        if (withCategory.Count == 0)
        {
            return;
        }

        var codes = withCategory.Select(r => r.CategoryCode!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var found = await db.QuestionCategories.AsNoTracking()
            .Where(c => codes.Contains(c.Code) && c.IsActive)
            .Select(c => new { c.Id, c.Code })
            .ToListAsync(ct);
        var byCode = found.ToDictionary(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var row in withCategory)
        {
            if (byCode.TryGetValue(row.CategoryCode!, out var id))
            {
                row.Input = row.Input! with { CategoryId = id };
            }
            else
            {
                row.Issues.Add(new QuestionImportIssue(
                    QuestionImportColumns.Category, "CATEGORY_NOT_FOUND", $"Không có danh mục đang hoạt động với mã \"{row.CategoryCode}\"."));
            }
        }
    }

    /// <summary>Mã câu hỏi không được trùng trong file và không trùng câu đã có.</summary>
    private async Task CheckCodesAsync(List<ImportRow> rows, CancellationToken ct)
    {
        var withCode = rows.Where(r => !string.IsNullOrWhiteSpace(r.Input?.Code)).ToList();
        if (withCode.Count == 0)
        {
            return;
        }

        var codes = withCode.Select(r => r.Input!.Code!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = (await db.Questions.AsNoTracking().Where(q => codes.Contains(q.Code)).Select(q => q.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in withCode)
        {
            var code = row.Input!.Code!.Trim();
            if (existing.Contains(code))
            {
                row.Issues.Add(new QuestionImportIssue(QuestionImportColumns.Code, "DUPLICATE_CODE", $"Mã câu hỏi \"{code}\" đã tồn tại."));
            }
            else if (!seen.Add(code))
            {
                row.Issues.Add(new QuestionImportIssue(
                    QuestionImportColumns.Code, "DUPLICATE_CODE_IN_FILE", $"Mã câu hỏi \"{code}\" bị trùng trong file."));
            }
        }
    }

    private static string? ColumnFor(string? field)
    {
        if (field is null)
        {
            return null;
        }

        foreach (var (prefix, column) in FieldColumns)
        {
            if (field.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return column;
            }
        }

        return field;
    }

    private static string Preview(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var text = content.ReplaceLineEndings(" ").Trim();
        return text.Length <= 120 ? text : text[..120] + "…";
    }

    private sealed class ImportRow(QuestionImportRawRow raw)
    {
        public QuestionImportRawRow Raw { get; } = raw;

        public QuestionInput? Input { get; set; }

        public string? CategoryCode { get; set; }

        public List<QuestionImportIssue> Issues { get; } = [];
    }
}
