using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Questions;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Danh mục câu hỏi (docs/05-api.md mục 6.3). Không có DELETE (D-16).</summary>
[Route("api/question-categories")]
public sealed class QuestionCategoriesController(ICategoryService categories) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.CategoryView)]
    [ProducesResponseType<ApiResponse<PagedResult<CategoryDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] CategoryListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await categories.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.CategoryView)]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => ToResponse(await categories.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.CategoryManage)]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateCategoryRequest request, CancellationToken ct) =>
        ToResponse(await categories.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CategoryManage)]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateCategoryRequest request, CancellationToken ct) =>
        ToResponse(await categories.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.CategoryManage)]
    [ProducesResponseType<ApiResponse<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetStatus(Guid id, SetActiveRequest request, CancellationToken ct) =>
        ToResponse(await categories.SetActiveAsync(id, request, ct));
}

/// <summary>Ngân hàng câu hỏi (docs/05-api.md mục 6.3). Không có DELETE (D-16).</summary>
[Route("api/questions")]
public sealed class QuestionsController(IQuestionService questions) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.QuestionView)]
    [ProducesResponseType<ApiResponse<PagedResult<QuestionListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] QuestionListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await questions.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.QuestionView)]
    [ProducesResponseType<ApiResponse<QuestionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => ToResponse(await questions.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.QuestionCreate)]
    [ProducesResponseType<ApiResponse<QuestionDetailDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(QuestionInput request, CancellationToken ct) =>
        ToResponse(await questions.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.QuestionUpdate)]
    [ProducesResponseType<ApiResponse<QuestionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateQuestionRequest request, CancellationToken ct) =>
        ToResponse(await questions.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.QuestionUpdate)]
    [ProducesResponseType<ApiResponse<QuestionDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetStatus(Guid id, SetActiveRequest request, CancellationToken ct) =>
        ToResponse(await questions.SetActiveAsync(id, request, ct));

    [HttpPost("{id:guid}/clone")]
    [HasPermission(Permissions.QuestionCreate)]
    [ProducesResponseType<ApiResponse<QuestionDetailDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Clone(Guid id, CancellationToken ct) =>
        ToResponse(await questions.CloneAsync(id, ct), StatusCodes.Status201Created);
}
