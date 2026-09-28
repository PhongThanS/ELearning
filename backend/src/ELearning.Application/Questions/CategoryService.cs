using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Questions;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Questions;

public sealed record CategoryListQuery : PageRequest
{
    public string? Keyword { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record CategoryDto(
    Guid Id, string Code, string Name, bool IsActive, int QuestionCount, DateTime CreatedAt, DateTime? UpdatedAt, string RowVersion);

public sealed record CreateCategoryRequest(string Code, string Name);

public sealed record UpdateCategoryRequest(string Name, bool IsActive, string RowVersion);

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> ListAsync(CategoryListQuery query, CancellationToken ct);

    Task<Result<CategoryDto>> GetAsync(Guid id, CancellationToken ct);

    Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken ct);

    Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct);

    Task<Result<CategoryDto>> SetActiveAsync(Guid id, SetActiveRequest request, CancellationToken ct);
}

internal sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryValidator()
    {
        RuleFor(r => r.Code).NotEmpty().WithErrorCode("CODE_REQUIRED").WithMessage("Vui lòng nhập mã danh mục.")
            .Matches("^[A-Za-z0-9#+._-]{1,100}$").WithErrorCode("CODE_INVALID")
            .WithMessage("Mã danh mục gồm tối đa 100 ký tự: chữ không dấu, số và # + . _ -");
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên danh mục.").MaximumLength(200);
    }
}

internal sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithErrorCode("NAME_REQUIRED").WithMessage("Vui lòng nhập tên danh mục.").MaximumLength(200);
        RuleFor(r => r.RowVersion).NotEmpty().WithErrorCode("ROWVERSION_REQUIRED").WithMessage("Thiếu rowVersion.");
    }
}

/// <summary>Danh mục câu hỏi; không xóa cứng, chỉ bật/tắt (D-16).</summary>
internal sealed class CategoryService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator) : ICategoryService
{
    private static readonly Error CategoryNotFound = Error.NotFound(message: "Không tìm thấy danh mục.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<CategoryDto>> ListAsync(CategoryListQuery query, CancellationToken ct)
    {
        var categories = db.QuestionCategories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            categories = categories.Where(c => EF.Functions.Like(c.Code, kw) || EF.Functions.Like(c.Name, kw));
        }

        if (query.IsActive is { } isActive)
        {
            categories = categories.Where(c => c.IsActive == isActive);
        }

        categories = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", false) => categories.OrderBy(c => c.Code),
            ("code", true) => categories.OrderByDescending(c => c.Code),
            ("createdat", false) => categories.OrderBy(c => c.CreatedAt),
            ("createdat", true) => categories.OrderByDescending(c => c.CreatedAt),
            (_, true) => categories.OrderByDescending(c => c.Name),
            _ => categories.OrderBy(c => c.Name),
        };

        var total = await categories.CountAsync(ct);
        var items = await Project(categories.Skip(query.Skip).Take(query.PageSize)).ToListAsync(ct);
        return new PagedResult<CategoryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<CategoryDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await Project(db.QuestionCategories.AsNoTracking().Where(c => c.Id == id)).SingleOrDefaultAsync(ct);
        return dto is null ? CategoryNotFound : dto;
    }

    public async Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<CategoryDto>.Failure(errors);
        }

        var code = request.Code.Trim();
        if (await db.QuestionCategories.AnyAsync(c => c.Code == code, ct))
        {
            return Error.Conflict(ErrorCodes.DuplicateCode, "Mã danh mục đã tồn tại.");
        }

        var category = new QuestionCategory(code, request.Name, currentUser.RequiredUserId, Now);
        db.QuestionCategories.Add(category);
        audit.Write(AuditActions.CategoryCreated, nameof(QuestionCategory), category.Id, newValue: new { category.Code, category.Name });
        await db.SaveChangesAsync(ct);
        return await GetAsync(category.Id, ct);
    }

    public async Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<CategoryDto>.Failure(errors);
        }

        var category = await db.QuestionCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return CategoryNotFound;
        }

        if (!db.TryApplyRowVersion(category, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        var old = new { category.Name, category.IsActive };
        category.Update(request.Name, request.IsActive, Now);
        audit.Write(AuditActions.CategoryUpdated, nameof(QuestionCategory), category.Id, old, new { category.Name, category.IsActive });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<CategoryDto>> SetActiveAsync(Guid id, SetActiveRequest request, CancellationToken ct)
    {
        var category = await db.QuestionCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return CategoryNotFound;
        }

        if (category.IsActive != request.IsActive)
        {
            category.Update(category.Name, request.IsActive, Now);
            audit.Write(
                AuditActions.CategoryStatusChanged, nameof(QuestionCategory), category.Id, new { IsActive = !request.IsActive }, new { request.IsActive });
            await db.SaveChangesAsync(ct);
        }

        return await GetAsync(id, ct);
    }

    private IQueryable<CategoryDto> Project(IQueryable<QuestionCategory> categories) =>
        categories.Select(c => new CategoryDto(
            c.Id,
            c.Code,
            c.Name,
            c.IsActive,
            db.Questions.Count(q => q.CategoryId == c.Id),
            c.CreatedAt,
            c.UpdatedAt,
            Convert.ToBase64String(c.RowVersion)));
}
