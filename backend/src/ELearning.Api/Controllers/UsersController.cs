using ELearning.Api.Common;
using ELearning.Api.Security;
using ELearning.Application.Attempts;
using ELearning.Application.Auth;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Users;
using ELearning.Domain.Identity;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Controllers;

/// <summary>Quản trị người dùng (docs/05-api.md mục 6.2).</summary>
[Route("api/users")]
public sealed class UsersController(
    IUserService users,
    IRegistrationSettingService registrationSetting,
    ICurrentUser currentUser,
    IAttemptService attempts,
    IUserImportService imports) : ApiControllerBase
{
    private const long MaxImportBytes = 5 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("registration-setting")]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<RegistrationStatusDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetRegistrationSetting(CancellationToken ct) =>
        Ok(ApiResponse.Ok(new RegistrationStatusDto(await registrationSetting.IsSelfRegistrationAllowedAsync(ct)), TraceId));

    [HttpPatch("registration-setting")]
    [HasPermission(Permissions.UserUpdate)]
    [ProducesResponseType<ApiResponse<RegistrationStatusDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetRegistrationSetting([FromBody] SetRegistrationSettingRequest request, CancellationToken ct)
    {
        var result = await registrationSetting.SetSelfRegistrationAllowedAsync(request.AllowSelfRegistration, currentUser.UserId, ct);
        return Ok(ApiResponse.Ok(new RegistrationStatusDto(result), TraceId));
    }

    [HttpGet]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<PagedResult<UserListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> List([FromQuery] UserListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await users.ListAsync(query, ct), TraceId));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => ToResponse(await users.GetAsync(id, ct));

    /// <summary>File mẫu import tài khoản (kèm hướng dẫn, danh sách vai trò và mã lớp).</summary>
    [HttpGet("import/template")]
    [HasPermission(Permissions.UserCreate)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ImportTemplate(CancellationToken ct) =>
        File(await imports.CreateTemplateAsync(ct), XlsxContentType, "mau-import-tai-khoan.xlsx");

    /// <summary>
    /// Import tài khoản từ .xlsx (tối đa 5 MB, 500 tài khoản). dryRun=true chỉ kiểm tra. Tất cả hoặc không:
    /// có dòng lỗi thì không tạo tài khoản nào, kết quả trả về lỗi theo từng dòng.
    /// </summary>
    [HttpPost("import")]
    [HasPermission(Permissions.UserCreate)]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes + 64 * 1024)]
    [ProducesResponseType<ApiResponse<UserImportResult>>(StatusCodes.Status200OK)]
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

    [HttpPost]
    [HasPermission(Permissions.UserCreate)]
    [ProducesResponseType<ApiResponse<UserWithPasswordDto>>(StatusCodes.Status201Created)]
    public async Task<ActionResult> Create(CreateUserRequest request, CancellationToken ct) =>
        ToResponse(await users.CreateAsync(request, ct), StatusCodes.Status201Created);

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.UserUpdate)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Update(Guid id, UpdateUserRequest request, CancellationToken ct) =>
        ToResponse(await users.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.UserUpdate)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetStatus(Guid id, SetUserStatusRequest request, CancellationToken ct) =>
        ToResponse(await users.SetStatusAsync(id, request, ct));

    [HttpPut("{id:guid}/roles")]
    [HasPermission(Permissions.RoleAssign)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SetRoles(Guid id, SetUserRolesRequest request, CancellationToken ct) =>
        ToResponse(await users.SetRolesAsync(id, request, ct));

    [HttpPost("{id:guid}/reset-password")]
    [HasPermission(Permissions.UserResetPassword)]
    [ProducesResponseType<ApiResponse<UserWithPasswordDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ResetPassword(Guid id, CancellationToken ct) =>
        ToResponse(await users.ResetPasswordAsync(id, ct));

    [HttpPost("{id:guid}/anonymize")]
    [HasPermission(Permissions.UserAnonymize)]
    [ProducesResponseType<ApiResponse<UserDetailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Anonymize(Guid id, ReasonRequest request, CancellationToken ct) =>
        ToResponse(await users.AnonymizeAsync(id, request, ct));

    [HttpGet("{id:guid}/exams")]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<PagedResult<StudentExamListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListExams(Guid id, [FromQuery] StudentExamListQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await attempts.ListExamsAsync(id, query, ct), TraceId));

    [HttpGet("{id:guid}/history")]
    [HasPermission(Permissions.UserView)]
    [ProducesResponseType<ApiResponse<PagedResult<StudentHistoryItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> History(Guid id, [FromQuery] StudentHistoryQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await attempts.HistoryAsync(id, query, ct), TraceId));
}
