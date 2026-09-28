using System.Security.Cryptography;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Common;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Common;

public static class ValidationExtensions
{
    /// <summary>Chạy validator, trả về danh sách lỗi (rỗng nếu hợp lệ).</summary>
    public static async Task<IReadOnlyList<Error>> ValidateToErrorsAsync<T>(
        this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(instance, cancellationToken);
        return result.IsValid ? [] : ToErrors(result);
    }

    public static IReadOnlyList<Error> ToErrors(ValidationResult result) =>
        result.Errors.Select(e => Error.Validation(e.ErrorCode, e.ErrorMessage, ToCamelCase(e.PropertyName))).ToList();

    private static string? ToCamelCase(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // "Options[0].Content" → "options[0].content"
        return string.Join('.', name.Split('.').Select(p => p.Length == 0 ? p : char.ToLowerInvariant(p[0]) + p[1..]));
    }
}

public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    /// <summary>Chính sách mật khẩu (docs/07-bao-mat.md mục 2).</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("PASSWORD_REQUIRED").WithMessage("Vui lòng nhập mật khẩu.")
            .MinimumLength(MinLength).WithErrorCode("PASSWORD_TOO_SHORT")
            .WithMessage($"Mật khẩu phải có ít nhất {MinLength} ký tự.")
            .MaximumLength(MaxLength).WithErrorCode("PASSWORD_TOO_LONG")
            .WithMessage($"Mật khẩu không được vượt quá {MaxLength} ký tự.");

    /// <summary>Mật khẩu tạm 12 ký tự, bỏ các ký tự dễ nhầm (0/O, 1/l/I).</summary>
    public static string GenerateTemporary()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        return RandomNumberGenerator.GetString(alphabet, 12);
    }
}

public static class UserNameRules
{
    public const string Pattern = "^[A-Za-z0-9._-]{3,50}$";

    public static IRuleBuilderOptions<T, string> ValidUserName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("USERNAME_REQUIRED").WithMessage("Vui lòng nhập tên đăng nhập.")
            .Matches(Pattern).WithErrorCode("USERNAME_INVALID")
            .WithMessage("Tên đăng nhập gồm 3–50 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang.");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("EMAIL_REQUIRED").WithMessage("Vui lòng nhập email.")
            .MaximumLength(255).WithErrorCode("EMAIL_TOO_LONG").WithMessage("Email quá dài.")
            .EmailAddress().WithErrorCode("EMAIL_INVALID").WithMessage("Email không hợp lệ.");

    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithErrorCode("FULLNAME_REQUIRED").WithMessage("Vui lòng nhập họ tên.")
            .MaximumLength(200).WithErrorCode("FULLNAME_TOO_LONG").WithMessage("Họ tên không được vượt quá 200 ký tự.");
}

public static class TransactionExtensions
{
    /// <summary>
    /// Chạy một khối trong transaction, bọc bởi execution strategy (bắt buộc khi bật EnableRetryOnFailure,
    /// docs/10-bay-ky-thuat.md mục 5). Khối bên trong không được gọi dịch vụ ngoài.
    /// </summary>
    public static Task<T> InTransactionAsync<T>(this IAppDbContext db, Func<Task<T>> action, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var result = await action();
            await tx.CommitAsync(ct);
            return result;
        });
    }
}

public static class RowVersionExtensions
{
    /// <summary>
    /// Gán RowVersion client gửi lên làm giá trị gốc; SaveChanges sẽ ném DbUpdateConcurrencyException (→ 409)
    /// nếu dữ liệu đã bị người khác sửa.
    /// </summary>
    public static bool TryApplyRowVersion<TEntity>(this IAppDbContext db, TEntity entity, string? rowVersion)
        where TEntity : class, IHasRowVersion
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            return false;
        }

        db.Entry(entity).Property(e => e.RowVersion).OriginalValue = bytes;
        return true;
    }

    public static string ToBase64(this byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static Error MissingRowVersion() =>
        Error.Validation(ErrorCodes.ValidationFailed, "Thiếu hoặc sai rowVersion.", "rowVersion");
}
