using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Questions;
using ELearning.Domain.Enums;
using ELearning.Domain.Videos;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Videos;

public interface IVideoImportService
{
    Task<Result<VideoImportResult>> ImportAsync(Stream file, bool dryRun, CancellationToken ct);

    Task<byte[]> CreateTemplateAsync(CancellationToken ct);
}

/// <summary>
/// Thêm video bài giảng từ Excel (D-44). Mỗi dòng đi qua đúng validator của chức năng thêm video; chuyên đề, lớp, đề luyện
/// ghi bằng mã. Tiêu đề trống + link YouTube thì tự lấy tiêu đề từ YouTube. Tất cả hoặc không: có dòng lỗi thì không tạo video nào.
/// </summary>
internal sealed class VideoImportService(
    IAppDbContext db,
    IVideoImportWorkbook workbook,
    IVideoService videos,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateVideoRequest> validator) : IVideoImportService
{
    private static readonly char[] ListSeparators = [',', ';', '|', '\n'];

    private static readonly Dictionary<string, string> FieldColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = VideoImportColumns.Title,
        ["videoUrl"] = VideoImportColumns.VideoUrl,
        ["documentUrl"] = VideoImportColumns.DocumentUrl,
        ["homeworkUrl"] = VideoImportColumns.HomeworkUrl,
        ["classroomIds"] = VideoImportColumns.Classes,
    };

    public async Task<Result<VideoImportResult>> ImportAsync(Stream file, bool dryRun, CancellationToken ct)
    {
        QuestionImportSheet sheet;
        try
        {
            sheet = workbook.Read(file, VideoImportColumns.MaxRows);
        }
        catch (InvalidDataException ex)
        {
            return Error.Validation("IMPORT_FILE_INVALID", ex.Message, "file");
        }

        // Chỉ bắt buộc cột Link video: tiêu đề trống (hoặc không có cột) thì lấy từ YouTube
        var missing = new[] { VideoImportColumns.VideoUrl }
            .Where(h => !sheet.Headers.Contains(VideoImportColumns.Normalize(h)))
            .ToList();
        if (missing.Count > 0)
        {
            return Error.Validation("IMPORT_COLUMNS_MISSING", $"File thiếu cột: {string.Join(", ", missing)}. Hãy dùng file mẫu.", "file");
        }

        if (sheet.Rows.Count == 0)
        {
            return Error.Validation("IMPORT_EMPTY", "File không có dòng video nào.", "file");
        }

        var rows = sheet.Rows.Select(Map).ToList();
        await ResolveCodesAsync(rows, ct);
        await FillYoutubeTitlesAsync(rows, ct);

        foreach (var row in rows)
        {
            row.Request = new CreateVideoRequest(
                row.Title,
                row.VideoUrl,
                null,
                row.CategoryId,
                row.ClassIds,
                DocumentUrl: row.DocumentUrl,
                PracticeExamId: row.PracticeExamId,
                HomeworkUrl: row.HomeworkUrl);
            foreach (var error in await validator.ValidateToErrorsAsync(row.Request, ct))
            {
                // Tiêu đề trống đã báo lỗi (không lấy được từ YouTube): bỏ lỗi trùng của validator
                if (row.TitleReported && string.Equals(error.Field, "title", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                row.Issues.Add(new QuestionImportIssue(ColumnFor(error.Field), error.Code, error.Message));
            }
        }

        var validRows = rows.Count(r => r.Issues.Count == 0);
        if (dryRun || validRows != rows.Count)
        {
            return new VideoImportResult(dryRun, Imported: false, rows.Count, validRows, 0, rows.Select(r => r.ToResult()).ToList());
        }

        var now = time.GetUtcNow().UtcDateTime;
        var createdBy = currentUser.RequiredUserId;
        var created = new List<VideoLesson>();
        foreach (var row in rows)
        {
            var request = row.Request!;
            var video = new VideoLesson(
                request.Title.Trim(), request.VideoUrl.Trim(), request.DocumentUrl, request.Description, request.CategoryId,
                row.ClassIds, null, 0, createdBy, now);
            video.SetPracticeExam(request.PracticeExamId);
            video.SetHomework(request.HomeworkUrl);
            db.VideoLessons.Add(video);
            created.Add(video);
        }

        audit.Write(
            "Video.Imported",
            nameof(VideoLesson),
            newValue: new { Count = created.Count, Titles = created.Select(v => v.Title).ToList() });
        await db.SaveChangesAsync(ct);

        return new VideoImportResult(DryRun: false, Imported: true, rows.Count, validRows, created.Count, rows.Select(r => r.ToResult()).ToList());
    }

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct)
    {
        var classes = await db.Classrooms.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Code).Take(2000)
            .Select(c => new { c.Code, c.Name }).ToListAsync(ct);
        var categories = await db.QuestionCategories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Code).Take(2000)
            .Select(c => new { c.Code, c.Name }).ToListAsync(ct);
        var exams = await db.Exams.AsNoTracking().Where(e => e.Kind == ExamKind.Practice).OrderBy(e => e.Code).Take(2000)
            .Select(e => new { e.Code, e.Name }).ToListAsync(ct);
        return workbook.CreateTemplate(
            classes.Select(c => (c.Code, c.Name)).ToList(),
            categories.Select(c => (c.Code, c.Name)).ToList(),
            exams.Select(e => (e.Code, e.Name)).ToList());
    }

    private static ImportRow Map(QuestionImportRawRow raw)
    {
        string? Get(string column) =>
            raw.Cells.TryGetValue(VideoImportColumns.Normalize(column), out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        var row = new ImportRow(raw.RowNumber)
        {
            Title = Get(VideoImportColumns.Title) ?? string.Empty,
            VideoUrl = Get(VideoImportColumns.VideoUrl) ?? string.Empty,
            DocumentUrl = Get(VideoImportColumns.DocumentUrl),
            HomeworkUrl = Get(VideoImportColumns.HomeworkUrl),
            CategoryCode = Get(VideoImportColumns.Category),
            PracticeExamCode = Get(VideoImportColumns.PracticeExam),
        };
        row.ClassCodes.AddRange(Split(Get(VideoImportColumns.Classes)).Distinct(StringComparer.OrdinalIgnoreCase));
        return row;
    }

    /// <summary>Mã chuyên đề / lớp / đề luyện → id (không phân biệt hoa thường; lower() của PostgreSQL).</summary>
    private async Task ResolveCodesAsync(List<ImportRow> rows, CancellationToken ct)
    {
        var categoryCodes = rows.Where(r => r.CategoryCode is not null).Select(r => r.CategoryCode!.ToLowerInvariant()).Distinct().ToList();
        var classCodes = rows.SelectMany(r => r.ClassCodes).Select(c => c.ToLowerInvariant()).Distinct().ToList();
        var examCodes = rows.Where(r => r.PracticeExamCode is not null).Select(r => r.PracticeExamCode!.ToLowerInvariant()).Distinct().ToList();
#pragma warning disable CA1304, CA1311
        var categories = categoryCodes.Count == 0 ? [] : await db.QuestionCategories.AsNoTracking()
            .Where(c => categoryCodes.Contains(c.Code.ToLower()))
            .Select(c => new CodeRef(c.Id, c.Code, c.Name, c.IsActive)).ToListAsync(ct);
        var classes = classCodes.Count == 0 ? [] : await db.Classrooms.AsNoTracking()
            .Where(c => classCodes.Contains(c.Code.ToLower()))
            .Select(c => new CodeRef(c.Id, c.Code, c.Name, c.IsActive)).ToListAsync(ct);
        var exams = examCodes.Count == 0 ? [] : await db.Exams.AsNoTracking()
            .Where(e => examCodes.Contains(e.Code.ToLower()))
            .Select(e => new CodeRef(e.Id, e.Code, e.Name, e.Kind == ExamKind.Practice)).ToListAsync(ct);
#pragma warning restore CA1304, CA1311
        var categoryByCode = categories.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var classByCode = classes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var examByCode = exams.ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.CategoryCode is { } categoryCode)
            {
                if (categoryByCode.TryGetValue(categoryCode, out var category) && category.Ok)
                {
                    row.CategoryId = category.Id;
                    row.CategoryName = category.Name;
                }
                else
                {
                    row.Issues.Add(new QuestionImportIssue(
                        VideoImportColumns.Category, "CATEGORY_NOT_FOUND", $"Không có chuyên đề đang hoạt động với mã \"{categoryCode}\" (xem sheet ChuyenDe)."));
                }
            }

            foreach (var code in row.ClassCodes)
            {
                if (classByCode.TryGetValue(code, out var classroom) && classroom.Ok)
                {
                    row.ClassIds.Add(classroom.Id);
                    row.ClassNames.Add(classroom.Name);
                }
                else
                {
                    row.Issues.Add(new QuestionImportIssue(
                        VideoImportColumns.Classes, "CLASSROOM_NOT_FOUND", $"Không có lớp đang hoạt động với mã \"{code}\" (xem sheet Lop)."));
                }
            }

            if (row.PracticeExamCode is { } examCode)
            {
                if (!examByCode.TryGetValue(examCode, out var exam))
                {
                    row.Issues.Add(new QuestionImportIssue(
                        VideoImportColumns.PracticeExam, "PRACTICE_EXAM_INVALID", $"Không có đề với mã \"{examCode}\" (xem sheet DeLuyen)."));
                }
                else if (!exam.Ok)
                {
                    row.Issues.Add(new QuestionImportIssue(
                        VideoImportColumns.PracticeExam, "PRACTICE_EXAM_INVALID", $"Đề \"{examCode}\" không phải loại Đề luyện."));
                }
                else
                {
                    row.PracticeExamId = exam.Id;
                    row.PracticeExamName = exam.Name;
                }
            }
        }
    }

    /// <summary>Tiêu đề trống + link YouTube: lấy tiêu đề từ YouTube (oEmbed, có nhớ đệm); không lấy được thì báo lỗi dòng.</summary>
    private async Task FillYoutubeTitlesAsync(List<ImportRow> rows, CancellationToken ct)
    {
        foreach (var row in rows.Where(r => r.Title.Length == 0 && r.VideoUrl.Length > 0))
        {
            var info = await videos.GetYoutubeInfoAsync(row.VideoUrl, ct);
            if (info.IsSuccess)
            {
                row.Title = info.Value.Title.Length > 250 ? info.Value.Title[..250] : info.Value.Title;
            }
            else
            {
                row.Issues.Add(new QuestionImportIssue(
                    VideoImportColumns.Title, "TITLE_REQUIRED", "Chưa có tiêu đề: link không phải YouTube hoặc không lấy được tiêu đề từ YouTube (video riêng tư / đã xóa). Hãy ghi tiêu đề."));
                row.TitleReported = true;
            }
        }
    }

    private static string[] Split(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? ColumnFor(string? field) =>
        field is not null && FieldColumns.TryGetValue(field, out var column) ? column : field;

    private sealed record CodeRef(Guid Id, string Code, string Name, bool Ok);

    private sealed class ImportRow(int rowNumber)
    {
        public int RowNumber { get; } = rowNumber;

        public string Title { get; set; } = string.Empty;

        public bool TitleReported { get; set; }

        public string VideoUrl { get; init; } = string.Empty;

        public string? DocumentUrl { get; init; }

        public string? HomeworkUrl { get; init; }

        public string? CategoryCode { get; init; }

        public Guid? CategoryId { get; set; }

        public string? CategoryName { get; set; }

        public List<string> ClassCodes { get; } = [];

        public List<Guid> ClassIds { get; } = [];

        public List<string> ClassNames { get; } = [];

        public string? PracticeExamCode { get; init; }

        public Guid? PracticeExamId { get; set; }

        public string? PracticeExamName { get; set; }

        public CreateVideoRequest? Request { get; set; }

        public List<QuestionImportIssue> Issues { get; } = [];

        public VideoImportRowResult ToResult() =>
            new(
                RowNumber,
                Title.Length > 0 ? Title : null,
                VideoUrl.Length > 0 ? VideoUrl : null,
                CategoryName,
                ClassNames,
                PracticeExamName,
                Issues);
    }
}
