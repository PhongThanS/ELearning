using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Questions;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>
/// Danh sách câu hỏi bị học sinh báo lỗi (D-46, docs/05-api.md). Gom theo câu trong snapshot đề;
/// sửa đáp án / hủy câu vẫn làm ở trang kết quả đề thi (D-11).
/// </summary>
[Route("api/question-reports")]
[HasPermission(Permissions.QuestionReportManage)]
public sealed class QuestionReportsController(IQuestionReportService reports) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResult<ReportedQuestionDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] QuestionReportListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await reports.ListAsync(query, ct), TraceId));

    /// <summary>Số câu đang có báo lỗi chờ xử lý (huy hiệu trên menu).</summary>
    [HttpGet("open-count")]
    [ProducesResponseType<ApiResponse<int>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> OpenCount(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await reports.CountOpenQuestionsAsync(ct), TraceId));

    [HttpGet("{examQuestionId:guid}")]
    [ProducesResponseType<ApiResponse<ReportedQuestionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid examQuestionId, CancellationToken ct) =>
        ToResponse(await reports.GetAsync(examQuestionId, ct));

    [HttpPost("{examQuestionId:guid}/resolve")]
    [ProducesResponseType<ApiResponse<ResolveQuestionReportsResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Resolve(Guid examQuestionId, ResolveQuestionReportsRequest request, CancellationToken ct) =>
        ToResponse(await reports.ResolveAsync(examQuestionId, request, ct));
}
