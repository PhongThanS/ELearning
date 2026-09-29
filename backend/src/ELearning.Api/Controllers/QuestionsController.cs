using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Questions;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
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
public sealed class QuestionsController(IQuestionService questions, IQuestionImportService imports) : ApiControllerBase
{
    private const long MaxImportBytes = 5 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>File mẫu import câu hỏi (kèm hướng dẫn và danh sách mã danh mục).</summary>
    [HttpGet("import/template")]
    [HasPermission(Permissions.QuestionCreate)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ImportTemplate(CancellationToken ct) =>
        File(await imports.CreateTemplateAsync(ct), XlsxContentType, "mau-import-cau-hoi.xlsx");

    /// <summary>
    /// Import câu hỏi từ .xlsx (tối đa 5 MB, 1000 câu). dryRun=true chỉ kiểm tra. Tất cả hoặc không:
    /// có dòng lỗi thì không tạo câu nào, kết quả trả về lỗi theo từng dòng.
    /// </summary>
    [HttpPost("import")]
    [HasPermission(Permissions.QuestionCreate)]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<QuestionImportResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Import(IFormFile? file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Failure(Result.Failure(Error.Validation("IMPORT_FILE_REQUIRED", "Vui lòng chọn file Excel.", "file")));
        }

        if (file.Length > MaxImportBytes || !file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(Result.Failure(Error.Validation("IMPORT_FILE_INVALID", "Chỉ nhận file .xlsx, tối đa 5 MB.", "file")));
        }

        await using var stream = file.OpenReadStream();
        return ToResponse(await imports.ImportAsync(stream, dryRun, ct));
    }

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
