using ELearning.Application.Common.Abstractions;
using ELearning.Application.Common.Options;
using ELearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ELearning.Application.Monitoring;

public interface IAttemptBacklogQuery
{
    /// <summary>Đếm lượt đang làm và lượt quá hạn (đã hết ân hạn) mà job chưa nộp; cập nhật gauge của <see cref="OperationalMetrics"/>.</summary>
    Task<AttemptBacklog> MeasureAsync(CancellationToken ct);
}

/// <summary>Ba truy vấn nhỏ trên filtered index IX_ExamAttempts_InProgress_Expired (Status = IN_PROGRESS).</summary>
internal sealed class AttemptBacklogQuery(
    IAppDbContext db,
    TimeProvider time,
    IOptions<ExamOptions> examOptions,
    OperationalMetrics metrics) : IAttemptBacklogQuery
{
    public async Task<AttemptBacklog> MeasureAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var cutoff = now.AddSeconds(-examOptions.Value.SubmitGraceSeconds);
        var inProgress = db.ExamAttempts.AsNoTracking().Where(a => a.Status == AttemptStatus.InProgress);

        var total = await inProgress.CountAsync(ct);
        var overdue = await inProgress.CountAsync(a => a.ExpiredAt < cutoff, ct);
        var oldest = overdue == 0
            ? null
            : await inProgress.Where(a => a.ExpiredAt < cutoff)
                .OrderBy(a => a.ExpiredAt)
                .Select(a => (DateTime?)a.ExpiredAt)
                .FirstOrDefaultAsync(ct);

        var backlog = new AttemptBacklog(total, overdue, oldest, now);
        metrics.UpdateBacklog(backlog);
        return backlog;
    }
}
