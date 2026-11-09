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

    /// <summary>Chỉ lấy chuyên đề con của chuyên đề này.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>true: chỉ chuyên đề gốc (không có cha).</summary>
    public bool? RootOnly { get; init; }
}

/// <summary>
/// Chuyên đề 2 cấp. <c>QuestionCount</c>: câu trực thuộc chính chuyên đề; <c>TotalQuestionCount</c>: gồm cả câu của chuyên đề con.
/// </summary>
public sealed record CategoryDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    int QuestionCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion,
    Guid? ParentId = null,
    string? ParentName = null,
    int ChildCount = 0,
    int TotalQuestionCount = 0);

/// <summary><c>ParentId</c>: chuyên đề cha (phải là chuyên đề gốc); bỏ trống = chuyên đề gốc.</summary>
public sealed record CreateCategoryRequest(string Code, string Name, Guid? ParentId = null);

/// <summary><c>ParentId</c> null = chuyển thành chuyên đề gốc.</summary>
public sealed record UpdateCategoryRequest(string Name, bool IsActive, string RowVersion, Guid? ParentId = null);

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> ListAsync(CategoryListQuery query, CancellationToken ct);

    Task<Result<CategoryDto>> GetAsync(Guid id, CancellationToken ct);

    Task<Result<CategoryDto>> CreateAsync(CreateCategoryRequest request, CancellationToken ct);

    Task<Result<CategoryDto>> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct);

    Task<Result<CategoryDto>> SetActiveAsync(Guid id, SetActiveRequest request, CancellationToken ct);

    Task<Result> DeleteAsync(Guid id, bool force = false, CancellationToken ct = default);
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
            categories = categories.Where(c => EF.Functions.ILike(c.Code, kw) || EF.Functions.ILike(c.Name, kw));
        }

        if (query.IsActive is { } isActive)
        {
            categories = categories.Where(c => c.IsActive == isActive);
        }

        if (query.ParentId is { } parentId)
        {
            categories = categories.Where(c => c.ParentId == parentId);
        }

        if (query.RootOnly == true)
        {
            categories = categories.Where(c => c.ParentId == null);
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

        if (await ValidateParentAsync(null, request.ParentId, ct) is { } parentError)
        {
            return parentError;
        }

        var category = new QuestionCategory(code, request.Name, currentUser.RequiredUserId, Now, request.ParentId);
        db.QuestionCategories.Add(category);
        audit.Write(
            AuditActions.CategoryCreated, nameof(QuestionCategory), category.Id, newValue: new { category.Code, category.Name, category.ParentId });
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

        if (request.ParentId != category.ParentId && await ValidateParentAsync(category.Id, request.ParentId, ct) is { } parentError)
        {
            return parentError;
        }

        var old = new { category.Name, category.IsActive, category.ParentId };
        category.Update(request.Name, request.IsActive, Now);
        if (request.ParentId != category.ParentId)
        {
            category.MoveTo(request.ParentId, Now);
        }

        audit.Write(
            AuditActions.CategoryUpdated, nameof(QuestionCategory), category.Id, old, new { category.Name, category.IsActive, category.ParentId });
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

    public async Task<Result> DeleteAsync(Guid id, bool force = false, CancellationToken ct = default)
    {
        var category = await db.QuestionCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return CategoryNotFound;
        }

        var children = await db.QuestionCategories.CountAsync(c => c.ParentId == id, ct);
        if (children > 0)
        {
            return Error.Conflict(
                "CATEGORY_HAS_CHILDREN",
                $"Chuyên đề '{category.Name}' đang có {children} chuyên đề con. Hãy xóa hoặc chuyển các chuyên đề con sang chuyên đề khác trước.");
        }

        var questions = await db.Questions
            .Include(q => q.Options)
            .Include(q => q.AcceptedAnswers)
            .Include(q => q.Tags)
            .Where(q => q.CategoryId == id)
            .ToListAsync(ct);

        var examCount = await db.Exams.CountAsync(e => e.CategoryId == id, ct);
        if ((questions.Count > 0 || examCount > 0) && !force)
        {
            var exams = examCount > 0 ? $" và {examCount} đề thi (đề sẽ chuyển thành không có chuyên đề)" : string.Empty;
            return Error.Conflict(
                "CATEGORY_HAS_QUESTIONS",
                $"Chuyên đề '{category.Name}' đang có {questions.Count} câu hỏi trực thuộc{exams}. Vui lòng xác nhận xóa để xóa chuyên đề và các câu hỏi trực thuộc.");
        }

        // Đề thi giữ nguyên, chỉ bỏ chuyên đề (D-30)
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Exams\" SET \"CategoryId\" = NULL WHERE \"CategoryId\" = {id}", ct);

        if (questions.Count > 0)
        {
            // Gỡ liên kết SourceQuestionId ở ExamQuestions (nếu có) để snapshot bài thi không bị vỡ và không vi phạm ràng buộc FK
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ExamQuestions\" SET \"SourceQuestionId\" = NULL WHERE \"SourceQuestionId\" IN (SELECT \"Id\" FROM \"Questions\" WHERE \"CategoryId\" = {id})",
                ct);

            db.Questions.RemoveRange(questions);
        }

        // Gỡ liên kết CategoryId ở các ExamPoolRules (nếu có)
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"ExamPoolRules\" SET \"CategoryId\" = NULL WHERE \"CategoryId\" = {id}",
            ct);

        db.QuestionCategories.Remove(category);
        audit.Write(AuditActions.CategoryDeleted, nameof(QuestionCategory), category.Id, new { category.Code, category.Name }, null);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// Chỉ 2 cấp: cha phải tồn tại và là chuyên đề gốc; chuyên đề đang có con không được làm con của chuyên đề khác.
    /// </summary>
    private async Task<Error?> ValidateParentAsync(Guid? categoryId, Guid? parentId, CancellationToken ct)
    {
        if (parentId is not { } pid)
        {
            return null;
        }

        if (pid == categoryId)
        {
            return Error.Validation("PARENT_INVALID", "Chuyên đề không thể là cha của chính nó.", "parentId");
        }

        var parent = await db.QuestionCategories.AsNoTracking().Where(c => c.Id == pid).Select(c => new { c.ParentId }).SingleOrDefaultAsync(ct);
        if (parent is null)
        {
            return Error.Validation("PARENT_INVALID", "Chuyên đề cha không tồn tại.", "parentId");
        }

        if (parent.ParentId is not null)
        {
            return Error.Validation("PARENT_INVALID", "Chỉ chọn chuyên đề gốc làm chuyên đề cha (tối đa 2 cấp).", "parentId");
        }

        // Chuyên đề cha chỉ gom nhóm, không chứa câu hỏi (D-32)
        if (await db.Questions.CountAsync(q => q.CategoryId == pid, ct) is var parentQuestions and > 0)
        {
            return Error.Validation(
                "PARENT_HAS_QUESTIONS",
                $"Chuyên đề cha đang có {parentQuestions} câu hỏi. Chuyên đề có chuyên đề con không chứa câu hỏi — hãy chuyển các câu này sang chuyên đề khác trước.",
                "parentId");
        }

        if (categoryId is { } id && await db.QuestionCategories.AnyAsync(c => c.ParentId == id, ct))
        {
            return Error.Validation("PARENT_INVALID", "Chuyên đề đang có chuyên đề con nên không thể làm chuyên đề con.", "parentId");
        }

        return null;
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
            Convert.ToBase64String(c.RowVersion),
            c.ParentId,
            db.QuestionCategories.Where(p => p.Id == c.ParentId).Select(p => p.Name).FirstOrDefault(),
            db.QuestionCategories.Count(ch => ch.ParentId == c.Id),
            db.Questions.Count(q => q.CategoryId == c.Id
                || db.QuestionCategories.Any(ch => ch.Id == q.CategoryId && ch.ParentId == c.Id))));
}
