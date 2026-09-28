using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Admin;

public interface IReportService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct);

    Task<Result<IReadOnlyList<QuestionStatDto>>> GetQuestionStatisticsAsync(Guid examId, Guid? versionId, CancellationToken ct);

    Task<PagedResult<AuditLogDto>> ListAuditLogsAsync(AuditLogQuery query, CancellationToken ct);
}

/// <summary>Dashboard, thống kê câu hỏi, audit log (docs/05-api.md mục 6.8).</summary>
internal sealed class ReportService(
    IAppDbContext db, IReportQuery reports, TimeProvider time, IOptions<AppOptions> appOptions) : IReportService
{
    /// <summary>"Hôm nay" tính theo múi giờ nghiệp vụ (Asia/Ho_Chi_Minh), đổi ra khoảng UTC để truy vấn.</summary>
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(appOptions.Value.BusinessTimeZone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var startLocal = new DateTimeOffset(local.Date, local.Offset);
        return await reports.GetDashboardAsync(
            startLocal.UtcDateTime, startLocal.AddDays(1).UtcDateTime, now.UtcDateTime, ct);
    }

    public async Task<Result<IReadOnlyList<QuestionStatDto>>> GetQuestionStatisticsAsync(Guid examId, Guid? versionId, CancellationToken ct)
    {
        var version = await db.ExamVersions.AsNoTracking()
            .Where(v => v.ExamId == examId && (versionId == null ? v.Status == Domain.Enums.ExamVersionStatus.Published : v.Id == versionId))
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(ct);
        if (version is null)
        {
            return Error.NotFound(message: "Không tìm thấy phiên bản đề.");
        }

        var stats = await reports.GetQuestionStatisticsAsync(version.Value, ct);
        return Result<IReadOnlyList<QuestionStatDto>>.Success(stats);
    }

    public async Task<PagedResult<AuditLogDto>> ListAuditLogsAsync(AuditLogQuery query, CancellationToken ct)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (query.UserId is { } userId)
        {
            logs = logs.Where(l => l.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim().ToUpperInvariant();
            logs = logs.Where(l => l.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            logs = logs.Where(l => l.EntityName == query.EntityName);
        }

        if (query.EntityId is { } entityId)
        {
            logs = logs.Where(l => l.EntityId == entityId);
        }

        if (query.From is { } from)
        {
            logs = logs.Where(l => l.CreatedAt >= from);
        }

        if (query.To is { } to)
        {
            logs = logs.Where(l => l.CreatedAt < to);
        }

        var total = await logs.CountAsync(ct);
        var items = await logs.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Skip(query.Skip).Take(query.PageSize)
            .Select(l => new AuditLogDto(
                l.Id,
                l.CreatedAt,
                l.UserId,
                db.Users.Where(u => u.Id == l.UserId).Select(u => u.UserName).FirstOrDefault(),
                l.Action,
                l.EntityName,
                l.EntityId,
                l.OldValue,
                l.NewValue,
                l.Reason,
                l.IpAddress,
                l.TraceId))
            .ToListAsync(ct);
        return new PagedResult<AuditLogDto>(items, query.Page, query.PageSize, total);
    }
}
