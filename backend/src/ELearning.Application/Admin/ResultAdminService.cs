using ELearning.Application.Audit;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Admin;

public interface IResultAdminService
{
    Task<Result<PagedResult<AdminResultRowDto>>> ListAsync(Guid examId, AdminResultQuery query, CancellationToken ct);

    Task<Result<ExportFile>> ExportAsync(Guid examId, bool officialOnly, CancellationToken ct);
}

/// <summary>Danh sách và export kết quả (docs/05-api.md mục 6.8). Lượt bị hủy không bao giờ là điểm chính thức.</summary>
internal sealed class ResultAdminService(
    IAppDbContext db, IResultExporter exporter, IAuditService audit, TimeProvider time) : IResultAdminService
{
    public const int MaxExportRows = 20_000;

    public async Task<Result<PagedResult<AdminResultRowDto>>> ListAsync(Guid examId, AdminResultQuery query, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return Error.NotFound(ErrorCodes.ExamNotFound, "Không tìm thấy đề thi.");
        }

        var rows = await LoadRowsAsync(exam, query.Keyword, ct);
        if (query.Official)
        {
            rows = rows.Where(r => r.IsOfficial).ToList();
        }

        IEnumerable<AdminResultRowDto> sorted = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("totalscore", false) => rows.OrderBy(r => r.TotalScore),
            ("totalscore", true) => rows.OrderByDescending(r => r.TotalScore),
            ("username", true) => rows.OrderByDescending(r => r.UserName, StringComparer.OrdinalIgnoreCase),
            ("username", false) => rows.OrderBy(r => r.UserName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.AttemptNumber),
            ("submittedat", false) => rows.OrderBy(r => r.SubmittedAt),
            _ => rows.OrderByDescending(r => r.SubmittedAt),
        };

        var list = sorted.ToList();
        return new PagedResult<AdminResultRowDto>(
            list.Skip(query.Skip).Take(query.PageSize).ToList(), query.Page, query.PageSize, list.Count);
    }

    public async Task<Result<ExportFile>> ExportAsync(Guid examId, bool officialOnly, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return Error.NotFound(ErrorCodes.ExamNotFound, "Không tìm thấy đề thi.");
        }

        var rows = await LoadRowsAsync(exam, null, ct);
        if (officialOnly)
        {
            rows = rows.Where(r => r.IsOfficial).ToList();
        }

        rows = [.. rows.OrderBy(r => r.UserName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.AttemptNumber).Take(MaxExportRows)];
        var content = exporter.Export(exam.Code, exam.Name, rows);
        audit.Write(AuditActions.ResultsExported, nameof(Exam), exam.Id, newValue: new { officialOnly, Rows = rows.Count });
        await db.SaveChangesAsync(ct);

        var stamp = time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture);
        return new ExportFile(
            $"ket-qua-{exam.Code}-{stamp}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            content);
    }

    /// <summary>Mọi kết quả của đề; đánh dấu điểm chính thức theo RetakeScoringPolicy (D-08).</summary>
    private async Task<List<AdminResultRowDto>> LoadRowsAsync(Exam exam, string? keyword, CancellationToken ct)
    {
        var query = db.ExamResults.AsNoTracking().Where(r => r.ExamId == exam.Id)
            .Join(db.ExamAttempts, r => r.AttemptId, a => a.Id, (r, a) => new { r, a })
            .Join(db.Users, x => x.r.UserId, u => u.Id, (x, u) => new { x.r, x.a, u });
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = Common.Like.Contains(keyword);
            query = query.Where(x => EF.Functions.Like(x.u.UserName, kw) || EF.Functions.Like(x.u.FullName, kw));
        }

        var raw = await query.Select(x => new
        {
            x.r.AttemptId,
            x.u.Id,
            x.u.UserName,
            x.u.FullName,
            x.u.Email,
            x.a.AttemptNumber,
            x.a.Status,
            x.r.StartedAt,
            x.r.SubmittedAt,
            x.r.DurationSeconds,
            x.r.TotalScore,
            x.r.MaxScore,
            x.r.Percentage,
            x.r.CorrectCount,
            x.r.TotalQuestion,
            x.r.Passed,
            x.r.PendingManualCount,
        }).ToListAsync(ct);

        var official = raw
            .Where(r => r.Status != AttemptStatus.Cancelled)
            .GroupBy(r => r.Id)
            .Select(g => exam.RetakeScoringPolicy == RetakeScoringPolicy.Highest
                ? g.OrderByDescending(r => r.TotalScore).ThenByDescending(r => r.SubmittedAt).First().AttemptId
                : g.OrderByDescending(r => r.SubmittedAt).First().AttemptId)
            .ToHashSet();

        return raw.Select(r => new AdminResultRowDto(
            r.AttemptId, r.Id, r.UserName, r.FullName, r.Email, r.AttemptNumber, r.Status, r.StartedAt, r.SubmittedAt,
            r.DurationSeconds, r.TotalScore, r.MaxScore, r.Percentage, r.CorrectCount, r.TotalQuestion, r.Passed,
            official.Contains(r.AttemptId), r.PendingManualCount)).ToList();
    }
}
