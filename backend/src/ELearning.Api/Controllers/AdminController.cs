using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Admin;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Admin: lượt thi, kết quả, export, báo cáo, audit log, sửa đáp án (docs/05-api.md mục 6.6, 6.8).</summary>
[Route("api")]
public sealed class AdminController(
    IAdminAttemptService attempts,
    IResultAdminService results,
    IAnswerKeyService answerKeys,
    IReportService reports) : ApiControllerBase
{
    // ----- Lượt thi -----

    [HttpGet("admin/exams/{examId:guid}/attempts")]
    [HasPermission(Permissions.AttemptView)]
    [ProducesResponseType<ApiResponse<PagedResult<AdminAttemptListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListAttempts(Guid examId, [FromQuery] AdminAttemptListQuery query, CancellationToken ct) =>
        ToResponse(await attempts.ListAsync(examId, query, ct));

    [HttpGet("admin/attempts/{attemptId:guid}")]
    [HasPermission(Permissions.AttemptView)]
    [ProducesResponseType<ApiResponse<AdminAttemptDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAttempt(Guid attemptId, CancellationToken ct) => ToResponse(await attempts.GetAsync(attemptId, ct));

    [HttpPost("admin/attempts/{attemptId:guid}/extend")]
    [HasPermission(Permissions.AttemptManage)]
    [ProducesResponseType<ApiResponse<AdminAttemptDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Extend(Guid attemptId, ExtendAttemptRequest request, CancellationToken ct) =>
        ToResponse(await attempts.ExtendAsync(attemptId, request, ct));

    [HttpPost("admin/attempts/{attemptId:guid}/force-submit")]
    [HasPermission(Permissions.AttemptManage)]
    [ProducesResponseType<ApiResponse<AdminAttemptDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ForceSubmit(Guid attemptId, AdminReasonRequest request, CancellationToken ct) =>
        ToResponse(await attempts.ForceSubmitAsync(attemptId, request, ct));

    [HttpPost("admin/attempts/{attemptId:guid}/cancel")]
    [HasPermission(Permissions.AttemptManage)]
    [ProducesResponseType<ApiResponse<AdminAttemptDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Cancel(Guid attemptId, AdminReasonRequest request, CancellationToken ct) =>
        ToResponse(await attempts.CancelAsync(attemptId, request, ct));

    // ----- Kết quả -----

    [HttpGet("admin/exams/{examId:guid}/results")]
    [HasPermission(Permissions.ResultView)]
    [ProducesResponseType<ApiResponse<PagedResult<AdminResultRowDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListResults(Guid examId, [FromQuery] AdminResultQuery query, CancellationToken ct) =>
        ToResponse(await results.ListAsync(examId, query, ct));

    [HttpGet("admin/exams/{examId:guid}/results/export")]
    [HasPermission(Permissions.ResultExport)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Export(Guid examId, [FromQuery] bool official, CancellationToken ct)
    {
        var result = await results.ExportAsync(examId, official, ct);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Failure(result);
    }

    // ----- Sửa đáp án / chấm lại (D-11) -----

    [HttpPost("exams/{examId:guid}/versions/{versionId:guid}/questions/{examQuestionId:guid}/answer-key")]
    [HasPermission(Permissions.ExamRegrade)]
    [ProducesResponseType<ApiResponse<RegradeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> CorrectAnswerKey(
        Guid examId, Guid versionId, Guid examQuestionId, CorrectAnswerKeyRequest request, CancellationToken ct) =>
        ToResponse(await answerKeys.CorrectAnswerKeyAsync(examId, versionId, examQuestionId, request, ct));

    [HttpPost("exams/{examId:guid}/versions/{versionId:guid}/questions/{examQuestionId:guid}/void")]
    [HasPermission(Permissions.ExamRegrade)]
    [ProducesResponseType<ApiResponse<RegradeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> VoidQuestion(
        Guid examId, Guid versionId, Guid examQuestionId, AdminReasonRequest request, CancellationToken ct) =>
        ToResponse(await answerKeys.VoidQuestionAsync(examId, versionId, examQuestionId, request, ct));

    [HttpGet("exams/{examId:guid}/answer-key-corrections")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<AnswerKeyCorrectionDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListCorrections(Guid examId, CancellationToken ct) =>
        ToResponse(await answerKeys.ListCorrectionsAsync(examId, ct));

    // ----- Báo cáo và audit -----

    [HttpGet("admin/dashboard")]
    [HasPermission(Permissions.ReportView)]
    [ProducesResponseType<ApiResponse<DashboardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Dashboard(CancellationToken ct) => Ok(ApiResponse.Ok(await reports.GetDashboardAsync(ct), TraceId));

    [HttpGet("admin/reports/question-statistics")]
    [HasPermission(Permissions.ReportView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<QuestionStatDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> QuestionStatistics([FromQuery] Guid examId, [FromQuery] Guid? versionId, CancellationToken ct) =>
        ToResponse(await reports.GetQuestionStatisticsAsync(examId, versionId, ct));

    [HttpGet("admin/audit-logs")]
    [HasPermission(Permissions.AuditView)]
    [ProducesResponseType<ApiResponse<PagedResult<AuditLogDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> AuditLogs([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await reports.ListAuditLogsAsync(query, ct), TraceId));
}
