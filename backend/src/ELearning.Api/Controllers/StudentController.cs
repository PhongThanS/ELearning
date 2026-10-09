using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Attempts;
using ELearning.Application.Classes;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Questions;
using ELearning.Application.Videos;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ELearning.Api.Controllers;

/// <summary>
/// API của học viên (docs/05-api.md mục 6.7). Chỉ thấy đề được phép; tài nguyên của người khác → 404.
/// DTO không bao giờ chứa đáp án khi lượt thi đang làm.
/// </summary>
[Route("api/student")]
[Authorize(Policy = Policies.StudentOnly)]
public sealed class StudentController(
    IAttemptService attempts,
    IClassroomService classrooms,
    ICategoryService categories,
    IVideoService videos,
    ICurrentUser currentUser) : ApiControllerBase
{
    private Guid UserId => currentUser.RequiredUserId;

    /// <summary>Danh mục chuyên đề đang hoạt động (cho học sinh lọc video/đề thi).</summary>
    [HttpGet("categories")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<CategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Categories(CancellationToken ct) =>
        Ok(ApiResponse.Ok((await categories.ListAsync(new CategoryListQuery { IsActive = true, PageSize = 100 }, ct)).Items, TraceId));

    /// <summary>Các lớp đang hoạt động mà học viên thuộc về (D-28).</summary>
    [HttpGet("classes")]
    [ProducesResponseType<ApiResponse<IReadOnlyList<MyClassroomDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> MyClasses(CancellationToken ct) =>
        Ok(ApiResponse.Ok(await classrooms.ListMineAsync(UserId, ct), TraceId));

    [HttpGet("exams")]
    [ProducesResponseType<ApiResponse<PagedResult<StudentExamListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListExams([FromQuery] StudentExamListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await attempts.ListExamsAsync(UserId, query, ct), TraceId));

    [HttpGet("exams/{examId:guid}")]
    [ProducesResponseType<ApiResponse<StudentExamDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetExam(Guid examId, CancellationToken ct) =>
        ToResponse(await attempts.GetExamAsync(UserId, examId, ct));

    /// <summary>201 khi tạo lượt mới; 200 kèm resumed = true khi trả về lượt đang làm (D-07).</summary>
    [HttpPost("exams/{examId:guid}/start")]
    [EnableRateLimiting(RateLimitPolicies.AttemptAction)]
    [ProducesResponseType<ApiResponse<AttemptDto>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<AttemptDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Start(Guid examId, CancellationToken ct)
    {
        var result = await attempts.StartAsync(UserId, examId, ct);
        return result.IsSuccess && !result.Value.Resumed
            ? ToResponse(result, StatusCodes.Status201Created)
            : ToResponse(result);
    }

    [HttpGet("attempts/{attemptId:guid}")]
    [ProducesResponseType<ApiResponse<AttemptDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAttempt(Guid attemptId, CancellationToken ct) =>
        ToResponse(await attempts.GetAttemptAsync(UserId, attemptId, ct));

    [HttpPut("attempts/{attemptId:guid}/answers")]
    [EnableRateLimiting(RateLimitPolicies.AttemptWrite)]
    [ProducesResponseType<ApiResponse<SaveAnswersResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SaveAnswers(Guid attemptId, SaveAnswersRequest request, CancellationToken ct) =>
        ToResponse(await attempts.SaveAnswersAsync(UserId, attemptId, request, ct));

    [HttpPost("attempts/{attemptId:guid}/events")]
    [EnableRateLimiting(RateLimitPolicies.AttemptWrite)]
    [ProducesResponseType<ApiResponse<RecordEventsResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RecordEvents(Guid attemptId, RecordEventsRequest request, CancellationToken ct) =>
        ToResponse(await attempts.RecordEventsAsync(UserId, attemptId, request, ct));

    [HttpPost("attempts/{attemptId:guid}/submit")]
    [EnableRateLimiting(RateLimitPolicies.AttemptAction)]
    [ProducesResponseType<ApiResponse<StudentResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Submit(Guid attemptId, CancellationToken ct) =>
        ToResponse(await attempts.SubmitAsync(UserId, attemptId, ct));

    [HttpGet("attempts/{attemptId:guid}/result")]
    [ProducesResponseType<ApiResponse<StudentResultDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetResult(Guid attemptId, CancellationToken ct) =>
        ToResponse(await attempts.GetResultAsync(UserId, attemptId, ct));

    [HttpGet("history")]
    [ProducesResponseType<ApiResponse<PagedResult<StudentHistoryItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> History([FromQuery] StudentHistoryQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await attempts.HistoryAsync(UserId, query, ct), TraceId));

    [HttpGet("videos")]
    [ProducesResponseType<ApiResponse<PagedResult<VideoLessonDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListVideos([FromQuery] VideoListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await videos.ListForStudentAsync(query, ct), TraceId));

    [HttpGet("videos/{id:guid}")]
    [ProducesResponseType<ApiResponse<VideoLessonDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetVideo(Guid id, CancellationToken ct) =>
        ToResponse(await videos.GetAsync(id, ct));
}
