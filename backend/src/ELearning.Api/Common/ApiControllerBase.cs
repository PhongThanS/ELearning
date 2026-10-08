using System.Diagnostics;
using ELearning.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace ELearning.Api.Common;

/// <summary>
/// Controller mỏng: gọi application service rồi chuyển Result thành ApiResponse.
/// Không đặt logic nghiệp vụ trong controller.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected string TraceId => Activity.Current?.Id ?? HttpContext.TraceIdentifier;

    protected ActionResult ToResponse<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK) =>
        result.IsSuccess
            ? StatusCode(successStatusCode, ApiResponse.Ok(result.Value, TraceId))
            : Failure(result);

    protected ActionResult ToResponse(Result result, int successStatusCode = StatusCodes.Status204NoContent) =>
        result.IsSuccess
            ? StatusCode(successStatusCode)
            : Failure(result);

    protected ActionResult Failure(Result result)
    {
        var status = ErrorMapping.ToStatusCode(result.FirstError.Type);
        var message = result.FirstError.Type == ErrorType.Validation ? ApiResponse.ValidationMessage : null;
        return StatusCode(status, ApiResponse.Fail(result.Errors, TraceId, message));
    }
}
