using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Application.Media;
using ELearning.Domain.Common;
using ELearning.Domain.Questions;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Questions;

/// <summary>Nhập đề từ file "elearning-exam/1". Ảnh trong file phải được thay bằng <c>media:&lt;id&gt;</c> trước khi nhập thật.</summary>
public sealed record ExamMarkdownImportRequest(string? Markdown, Guid? CategoryId, bool DryRun);

public sealed record ExamMarkdownQuestionResult(
    int Index,
    int Line,
    string Label,
    string? Part,
    Guid? QuestionId,
    string? Code,
    QuestionInput? Question,
    IReadOnlyList<MarkdownImportIssue> Issues);

public sealed record ExamMarkdownImportResult(
    bool DryRun,
    bool Imported,
    ExamMarkdownHeader Exam,
    Guid? CategoryId,
    int TotalQuestions,
    int ValidQuestions,
    decimal TotalScore,
    IReadOnlyList<ExamMarkdownQuestionResult> Questions,
    IReadOnlyList<MarkdownImportIssue> FileIssues,
    IReadOnlyList<string> MissingImages);

public interface IExamMarkdownImportService
{
    Task<Result<ExamMarkdownImportResult>> ImportAsync(ExamMarkdownImportRequest request, CancellationToken ct);

    /// <summary>Lấy ảnh từ file đề Word gốc, đặt tên theo phần / câu để ghép với file .md. Không lưu gì.</summary>
    Task<Result<DocxImageResult>> ExtractDocxImagesAsync(Stream docx, CancellationToken ct);
}

/// <summary>
/// Nhập câu hỏi từ file đề Markdown (docs/02-nghiep-vu.md mục 1.5, D-29). Giống import Excel: mỗi câu đi qua đúng
/// validator và domain của chức năng tạo câu hỏi; tất cả hoặc không. Bản kiểm tra (dryRun) trả đủ nội dung từng câu
/// để xem trước; ảnh còn là tên file (chưa đính kèm) hoặc ảnh chờ tải (<c>media:pending-…</c>) chỉ được phép khi kiểm tra.
/// </summary>
internal sealed class ExamMarkdownImportService(
    IAppDbContext db,
    ICodeGenerator codeGenerator,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IMediaService media,
    IOptions<MediaOptions> mediaOptions,
    IValidator<QuestionInput> validator) : IExamMarkdownImportService
{
    private const string PendingPrefix = "media:pending-";

    public async Task<Result<DocxImageResult>> ExtractDocxImagesAsync(Stream docx, CancellationToken ct)
    {
        // ZipArchive cần đọc ngẫu nhiên; file đã được giới hạn kích thước ở controller
        using var buffer = new MemoryStream();
        await docx.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var result = DocxImageExtractor.Extract(buffer, mediaOptions.Value.MaxBytes);
        return result is null
            ? Error.Validation("IMPORT_FILE_INVALID", "File không phải file Word .docx hợp lệ (file .doc cũ: mở bằng Word và lưu lại thành .docx).", "file")
            : result;
    }

    public async Task<Result<ExamMarkdownImportResult>> ImportAsync(ExamMarkdownImportRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Markdown))
        {
            return Error.Validation("IMPORT_FILE_REQUIRED", "Vui lòng chọn file đề (.md).", "markdown");
        }

        if (request.Markdown.Length > ExamMarkdownParser.MaxLength)
        {
            return Error.Validation("IMPORT_FILE_INVALID", "File đề quá lớn (tối đa khoảng 2 MB văn bản).", "markdown");
        }

        var doc = ExamMarkdownParser.Parse(request.Markdown);
        var fileIssues = doc.FileIssues.ToList();
        var categoryId = await ResolveCategoryAsync(request.CategoryId, doc.Header.CategoryCode, fileIssues, ct);
        var missingImages = new List<string>();

        foreach (var q in doc.Questions.Where(q => q.Input is not null))
        {
            q.Input = q.Input! with { CategoryId = categoryId };
            CheckImages(q, request.DryRun, missingImages);
        }

        await CheckCodesAsync(doc.Questions, ct);
        foreach (var q in doc.Questions.Where(q => q.Input is not null && q.Issues.Count == 0))
        {
            await ValidateAsync(q, ct);
        }

        var valid = doc.Questions.Count(q => q.Input is not null && q.Issues.Count == 0);
        var ok = fileIssues.Count == 0 && valid == doc.Questions.Count && missingImages.Count == 0;
        if (request.DryRun || !ok)
        {
            return BuildResult(request.DryRun, imported: false, doc, categoryId, valid, fileIssues, missingImages, created: null);
        }

        var now = time.GetUtcNow().UtcDateTime;
        var created = await db.InTransactionAsync(
            async () =>
            {
                var questions = new List<Question>();
                foreach (var input in doc.Questions.Select(q => q.Input!))
                {
                    var code = string.IsNullOrWhiteSpace(input.Code) ? await codeGenerator.NextQuestionCodeAsync(ct) : input.Code.Trim();
                    var question = Question.Create(code, QuestionService.ToData(input), currentUser.RequiredUserId, now);
                    db.Questions.Add(question);
                    questions.Add(question);
                }

                audit.Write(
                    AuditActions.QuestionsImported,
                    nameof(Question),
                    newValue: new { Source = "MARKDOWN", doc.Header.Title, Count = questions.Count, Codes = questions.Select(q => q.Code).ToList() });
                await db.SaveChangesAsync(ct);
                return questions;
            },
            ct);

        return BuildResult(dryRun: false, imported: true, doc, categoryId, valid, fileIssues, missingImages, created);
    }

    private static ExamMarkdownImportResult BuildResult(
        bool dryRun,
        bool imported,
        ExamMarkdownDocument doc,
        Guid? categoryId,
        int valid,
        List<MarkdownImportIssue> fileIssues,
        List<string> missingImages,
        List<Question>? created)
    {
        var questions = doc.Questions.Select((q, i) => new ExamMarkdownQuestionResult(
                i + 1,
                q.Line,
                q.Label,
                q.Part,
                created?[i].Id,
                created?[i].Code ?? q.Input?.Code,
                q.Input,
                q.Issues))
            .ToList();
        var totalScore = doc.Questions.Where(q => q.Input is not null).Sum(q => q.Input!.DefaultScore);
        return new ExamMarkdownImportResult(
            dryRun, imported, doc.Header, categoryId, doc.Questions.Count, valid, totalScore, questions, fileIssues,
            missingImages.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>Danh mục chọn trên giao diện được ưu tiên; nếu không chọn thì dùng @category trong file.</summary>
    private async Task<Guid?> ResolveCategoryAsync(Guid? requested, string? code, List<MarkdownImportIssue> issues, CancellationToken ct)
    {
        if (requested is { } id)
        {
            if (await db.QuestionCategories.AnyAsync(c => c.Id == id && c.IsActive, ct))
            {
                return id;
            }

            issues.Add(new(null, "categoryId", "CATEGORY_NOT_FOUND", "Danh mục đã chọn không tồn tại hoặc đã tắt."));
            return null;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        var found = await db.QuestionCategories.AsNoTracking()
            .Where(c => c.Code == trimmed && c.IsActive)
            .Select(c => (Guid?)c.Id)
            .SingleOrDefaultAsync(ct);
        if (found is null)
        {
            issues.Add(new(null, "category", "CATEGORY_NOT_FOUND", $"Không có danh mục đang hoạt động với mã \"{trimmed}\" (@category). Chọn danh mục khi nhập hoặc sửa mã."));
        }

        return found;
    }

    /// <summary>
    /// Ảnh phải là <c>media:&lt;id&gt;</c> khi nhập thật. Khi kiểm tra, cho phép ảnh chờ tải (<c>media:pending-…</c>);
    /// tên file chưa đính kèm được liệt kê để người dùng chọn ảnh; ảnh ngoài (http) không hiển thị được nên là lỗi.
    /// </summary>
    private static void CheckImages(ExamMarkdownQuestion q, bool dryRun, List<string> missing)
    {
        foreach (var text in QuestionService.MediaTexts(q.Input!))
        {
            foreach (var (_, target) in ExamMarkdownParser.ImageRefs(text))
            {
                if (target.StartsWith(PendingPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (!dryRun)
                    {
                        q.Issues.Add(new(q.Line, "content", "IMAGE_NOT_UPLOADED", $"Câu {q.Label}: ảnh chưa được tải lên."));
                    }
                }
                else if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    q.Issues.Add(new(q.Line, "content", "IMAGE_EXTERNAL",
                        $"Câu {q.Label}: không dùng ảnh từ trang ngoài ({target}). Tải ảnh về máy rồi đính kèm khi nhập."));
                }
                else if (!target.StartsWith("media:", StringComparison.OrdinalIgnoreCase))
                {
                    missing.Add(target);
                }
            }
        }
    }

    private async Task CheckCodesAsync(IReadOnlyList<ExamMarkdownQuestion> questions, CancellationToken ct)
    {
        var withCode = questions.Where(q => !string.IsNullOrWhiteSpace(q.Input?.Code)).ToList();
        if (withCode.Count == 0)
        {
            return;
        }

        var codes = withCode.Select(q => q.Input!.Code!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = (await db.Questions.AsNoTracking().Where(q => codes.Contains(q.Code)).Select(q => q.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var q in withCode)
        {
            var code = q.Input!.Code!.Trim();
            if (existing.Contains(code))
            {
                q.Issues.Add(new(q.Line, "code", "DUPLICATE_CODE", $"Câu {q.Label}: mã câu hỏi \"{code}\" đã tồn tại."));
            }
            else if (!seen.Add(code))
            {
                q.Issues.Add(new(q.Line, "code", "DUPLICATE_CODE_IN_FILE", $"Câu {q.Label}: mã câu hỏi \"{code}\" bị trùng trong file."));
            }
        }
    }

    private async Task ValidateAsync(ExamMarkdownQuestion q, CancellationToken ct)
    {
        foreach (var error in await validator.ValidateToErrorsAsync(q.Input!, ct))
        {
            q.Issues.Add(new(q.Line, error.Field, error.Code, $"Câu {q.Label}: {error.Message}"));
        }

        foreach (var error in await media.ValidateReferencesAsync(QuestionService.MediaTexts(q.Input!), "content", ct))
        {
            q.Issues.Add(new(q.Line, error.Field, error.Code, $"Câu {q.Label}: {error.Message}"));
        }

        if (q.Issues.Count > 0)
        {
            return;
        }

        // Lớp bảo vệ cuối: domain tự kiểm tra bất biến
        try
        {
            Question.Create("IMPORT-CHECK", QuestionService.ToData(q.Input!), Guid.Empty, DateTime.UnixEpoch);
        }
        catch (DomainException ex)
        {
            q.Issues.Add(new(q.Line, null, ex.Code, $"Câu {q.Label}: {ex.Message}"));
        }
    }
}
