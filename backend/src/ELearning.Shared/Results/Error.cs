namespace ELearning.Shared.Results;

public sealed record Error(ErrorType Type, string Code, string Message, string? Field = null)
{
    public static Error Validation(string code, string message, string? field = null) =>
        new(ErrorType.Validation, code, message, field);

    public static Error Unauthorized(string code, string message) => new(ErrorType.Unauthorized, code, message);

    public static Error Forbidden(string message = "Bạn không có quyền thực hiện thao tác này.") =>
        new(ErrorType.Forbidden, ErrorCodes.Forbidden, message);

    public static Error NotFound(string code = ErrorCodes.NotFound, string message = "Không tìm thấy dữ liệu.") =>
        new(ErrorType.NotFound, code, message);

    public static Error Conflict(string code, string message) => new(ErrorType.Conflict, code, message);

    public static Error Business(string code, string message) => new(ErrorType.BusinessRule, code, message);
}
