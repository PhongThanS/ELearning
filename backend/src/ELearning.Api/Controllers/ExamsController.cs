using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Exams;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Đề thi, phiên bản, câu hỏi trong phiên bản, gán đề (docs/05-api.md mục 6.4–6.5).</summary>
[Route("api/exams")]
public sealed class ExamsController(IExamService exams, IExamVersionService versions) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<PagedResult<ExamListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] ExamListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await exams.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<ExamDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => ToResponse(await exams.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.ExamCreate)]
    [ProducesResponseType<ApiResponse<ExamDetailDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateExamRequest request, CancellationToken ct) =>
        ToResponse(await exams.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<ExamDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateExamRequest request, CancellationToken ct) =>
        ToResponse(await exams.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.ExamDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await exams.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("{id:guid}/clone")]
    [HasPermission(Permissions.ExamCreate)]
    [ProducesResponseType<ApiResponse<ExamDetailDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Clone(Guid id, CloneExamRequest request, CancellationToken ct) =>
        ToResponse(await exams.CloneAsync(id, request, ct), StatusCodes.Status201Created);

    [HttpPost("{id:guid}/close")]
    [HasPermission(Permissions.ExamClose)]
    [ProducesResponseType<ApiResponse<ExamDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Close(Guid id, CloseExamRequest request, CancellationToken ct) =>
        ToResponse(await exams.CloseAsync(id, request, ct));

    [HttpPost("{id:guid}/reopen")]
    [HasPermission(Permissions.ExamClose)]
    [ProducesResponseType<ApiResponse<ExamDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Reopen(Guid id, CancellationToken ct) => ToResponse(await exams.ReopenAsync(id, ct));

    [HttpPatch("{id:guid}/review-policy")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<ExamDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetReviewPolicy(Guid id, SetReviewPolicyRequest request, CancellationToken ct) =>
        ToResponse(await exams.SetReviewPolicyAsync(id, request, ct));

    [HttpGet("{id:guid}/assignments")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<AssignmentsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAssignments(Guid id, CancellationToken ct) => ToResponse(await exams.GetAssignmentsAsync(id, ct));

    [HttpPut("{id:guid}/assignments")]
    [HasPermission(Permissions.ExamAssign)]
    [ProducesResponseType<ApiResponse<AssignmentsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetAssignments(Guid id, SetAssignmentsRequest request, CancellationToken ct) =>
        ToResponse(await exams.SetAssignmentsAsync(id, request, ct));

    [HttpGet("{id:guid}/user-overrides")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<PagedResult<UserOverrideListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListUserOverrides(Guid id, [FromQuery] PageRequest query, CancellationToken ct) =>
        ToResponse(await exams.ListUserOverridesAsync(id, query, ct));

    [HttpPut("{id:guid}/user-overrides/{userId:guid}")]
    [HasPermission(Permissions.AttemptManage)]
    [ProducesResponseType<ApiResponse<UserOverrideDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetUserOverride(Guid id, Guid userId, SetUserOverrideRequest request, CancellationToken ct) =>
        ToResponse(await exams.SetUserOverrideAsync(id, userId, request, ct));

    // ----- Phiên bản -----

    [HttpGet("{id:guid}/versions")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<IReadOnlyList<VersionSummaryDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListVersions(Guid id, CancellationToken ct) => ToResponse(await versions.ListAsync(id, ct));

    [HttpPost("{id:guid}/versions")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateVersion(Guid id, CreateVersionRequest request, CancellationToken ct) =>
        ToResponse(await versions.CreateAsync(id, request, ct), StatusCodes.Status201Created);

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetVersion(Guid id, Guid versionId, CancellationToken ct) =>
        ToResponse(await versions.GetAsync(id, versionId, ct));

    [HttpPut("{id:guid}/versions/{versionId:guid}")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateVersion(Guid id, Guid versionId, UpdateVersionRequest request, CancellationToken ct) =>
        ToResponse(await versions.UpdateAsync(id, versionId, request, ct));

    [HttpDelete("{id:guid}/versions/{versionId:guid}")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteVersion(Guid id, Guid versionId, CancellationToken ct)
    {
        var result = await versions.DeleteAsync(id, versionId, ct);
        return result.IsSuccess ? NoContent() : Failure(result);
    }

    [HttpPost("{id:guid}/versions/{versionId:guid}/pool-rules")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> AddPoolRule(Guid id, Guid versionId, AddPoolRuleRequest request, CancellationToken ct) =>
        ToResponse(await versions.AddPoolRuleAsync(id, versionId, request, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/pool-rules/{poolRuleId:guid}/refresh")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RefreshPoolRule(Guid id, Guid versionId, Guid poolRuleId, CancellationToken ct) =>
        ToResponse(await versions.RefreshPoolRuleAsync(id, versionId, poolRuleId, ct));

    [HttpDelete("{id:guid}/versions/{versionId:guid}/pool-rules/{poolRuleId:guid}")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemovePoolRule(Guid id, Guid versionId, Guid poolRuleId, CancellationToken ct) =>
        ToResponse(await versions.RemovePoolRuleAsync(id, versionId, poolRuleId, ct));

    [HttpGet("{id:guid}/versions/{versionId:guid}/preview")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<ExamPreviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Preview(Guid id, Guid versionId, CancellationToken ct) =>
        ToResponse(await versions.PreviewAsync(id, versionId, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/validate")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<PublishValidationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Validate(Guid id, Guid versionId, CancellationToken ct) =>
        ToResponse(await versions.ValidateAsync(id, versionId, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/publish")]
    [HasPermission(Permissions.ExamPublish)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Publish(Guid id, Guid versionId, CancellationToken ct) =>
        ToResponse(await versions.PublishAsync(id, versionId, ct));

    // ----- Câu hỏi trong phiên bản nháp -----

    [HttpGet("{id:guid}/versions/{versionId:guid}/questions")]
    [HasPermission(Permissions.ExamView)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListQuestions(Guid id, Guid versionId, CancellationToken ct) =>
        ToResponse(await versions.GetAsync(id, versionId, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/questions")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> AddQuestions(Guid id, Guid versionId, AddVersionQuestionsRequest request, CancellationToken ct) =>
        ToResponse(await versions.AddQuestionsAsync(id, versionId, request, ct));

    [HttpPatch("{id:guid}/versions/{versionId:guid}/questions/{examQuestionId:guid}")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateQuestion(
        Guid id, Guid versionId, Guid examQuestionId, UpdateVersionQuestionRequest request, CancellationToken ct) =>
        ToResponse(await versions.UpdateQuestionAsync(id, versionId, examQuestionId, request, ct));

    [HttpDelete("{id:guid}/versions/{versionId:guid}/questions/{examQuestionId:guid}")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemoveQuestion(Guid id, Guid versionId, Guid examQuestionId, CancellationToken ct) =>
        ToResponse(await versions.RemoveQuestionAsync(id, versionId, examQuestionId, ct));

    [HttpPut("{id:guid}/versions/{versionId:guid}/questions/order")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Reorder(Guid id, Guid versionId, ReorderQuestionsRequest request, CancellationToken ct) =>
        ToResponse(await versions.ReorderAsync(id, versionId, request, ct));

    [HttpPost("{id:guid}/versions/{versionId:guid}/questions/sync")]
    [HasPermission(Permissions.ExamUpdate)]
    [ProducesResponseType<ApiResponse<VersionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Sync(Guid id, Guid versionId, SyncQuestionsRequest request, CancellationToken ct) =>
        ToResponse(await versions.SyncAsync(id, versionId, request, ct));
}
