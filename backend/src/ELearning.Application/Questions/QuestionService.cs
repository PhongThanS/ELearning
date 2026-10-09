using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Media;
using ELearning.Domain.Enums;
using ELearning.Domain.Questions;
using ELearning.Shared;
using ELearning.Shared.Paging;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Questions;

public interface IQuestionService
{
    Task<PagedResult<QuestionListItemDto>> ListAsync(QuestionListQuery query, CancellationToken ct);

    Task<Result<QuestionDetailDto>> GetAsync(Guid id, CancellationToken ct);

    Task<Result<QuestionDetailDto>> CreateAsync(QuestionInput request, CancellationToken ct);

    Task<Result<QuestionDetailDto>> UpdateAsync(Guid id, UpdateQuestionRequest request, CancellationToken ct);

    Task<Result<QuestionDetailDto>> SetActiveAsync(Guid id, SetActiveRequest request, CancellationToken ct);

    Task<Result<QuestionDetailDto>> CloneAsync(Guid id, CancellationToken ct);
}

/// <summary>Sinh mã tự động từ SEQUENCE trong DB (an toàn khi nhiều request đồng thời).</summary>
public interface ICodeGenerator
{
    Task<string> NextQuestionCodeAsync(CancellationToken ct);
}

/// <summary>Ngân hàng câu hỏi (docs/02-nghiep-vu.md mục 1, docs/05-api.md mục 6.3). Không có xóa cứng (D-16).</summary>
internal sealed class QuestionService(
    IAppDbContext db,
    ICodeGenerator codeGenerator,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    IMediaService media,
    MediaLinks mediaLinks,
    IValidator<QuestionInput> createValidator,
    IValidator<UpdateQuestionRequest> updateValidator) : IQuestionService
{
    private static readonly Error QuestionNotFound = Error.NotFound(ErrorCodes.QuestionNotFound, "Không tìm thấy câu hỏi.");
    private static readonly string[] ChoiceCodes = ["A", "B", "C", "D", "E", "F", "G", "H", "I", "J"];

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<QuestionListItemDto>> ListAsync(QuestionListQuery query, CancellationToken ct)
    {
        var questions = db.Questions.AsNoTracking();
        if (query.CategoryId is { } categoryId)
        {
            questions = questions.Where(q => q.CategoryId == categoryId);
        }

        if (query.QuestionType is { } type)
        {
            questions = questions.Where(q => q.QuestionType == type);
        }

        if (query.IsActive is { } isActive)
        {
            questions = questions.Where(q => q.IsActive == isActive);
        }

        if (query.Difficulty is { } difficulty)
        {
            questions = questions.Where(q => q.Difficulty == difficulty);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = Question.NormalizeTag(query.Tag);
            questions = questions.Where(q => q.Tags.Any(t => t.Tag == tag));
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = Like.Contains(query.Keyword);
            questions = questions.Where(q => EF.Functions.ILike(q.Code, kw) || EF.Functions.ILike(q.Content, kw));
        }

        questions = (query.SortBy?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", false) => questions.OrderBy(q => q.Code),
            ("code", true) => questions.OrderByDescending(q => q.Code),
            ("updatedat", false) => questions.OrderBy(q => q.UpdatedAt ?? q.CreatedAt),
            ("updatedat", true) => questions.OrderByDescending(q => q.UpdatedAt ?? q.CreatedAt),
            ("createdat", false) => questions.OrderBy(q => q.CreatedAt),
            _ => questions.OrderByDescending(q => q.CreatedAt),
        };

        var total = await questions.CountAsync(ct);
        var items = await questions
            .Skip(query.Skip).Take(query.PageSize)
            .Select(q => new QuestionListItemDto(
                q.Id,
                q.Code,
                q.Content.Length > 200 ? q.Content.Substring(0, 200) : q.Content,
                q.QuestionType,
                q.AnswerDataType,
                q.CategoryId,
                db.QuestionCategories.Where(c => c.Id == q.CategoryId).Select(c => c.Name).FirstOrDefault(),
                q.DefaultScore,
                q.IsActive,
                q.CreatedAt,
                q.UpdatedAt,
                q.Difficulty,
                q.Tags.OrderBy(t => t.Tag).Select(t => t.Tag).ToList()))
            .ToListAsync(ct);

        return new PagedResult<QuestionListItemDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<QuestionDetailDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var question = await LoadAsync(id, tracking: false, ct);
        return question is null ? QuestionNotFound : await ToDetailAsync(question, ct);
    }

    public async Task<Result<QuestionDetailDto>> CreateAsync(QuestionInput request, CancellationToken ct)
    {
        var errors = await createValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count == 0)
        {
            errors = [.. await media.ValidateReferencesAsync(MediaTexts(request), "content", ct)];
        }

        if (errors.Count > 0)
        {
            return Result<QuestionDetailDto>.Failure(errors);
        }

        if (await ValidateCategoryAsync(request.CategoryId, ct) is { } categoryError)
        {
            return categoryError;
        }

        string code;
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            code = await codeGenerator.NextQuestionCodeAsync(ct);
        }
        else
        {
            code = request.Code.Trim();
            if (await db.Questions.AnyAsync(q => q.Code == code, ct))
            {
                return Error.Conflict(ErrorCodes.DuplicateCode, "Mã câu hỏi đã tồn tại.");
            }
        }

        var question = Question.Create(code, ToData(request), currentUser.RequiredUserId, Now);
        db.Questions.Add(question);
        audit.Write(AuditActions.QuestionCreated, nameof(Question), question.Id, newValue: new { question.Code, question.QuestionType });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(question, ct);
    }

    public async Task<Result<QuestionDetailDto>> UpdateAsync(Guid id, UpdateQuestionRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count == 0)
        {
            errors = [.. await media.ValidateReferencesAsync(MediaTexts(request), "content", ct)];
        }

        if (errors.Count > 0)
        {
            return Result<QuestionDetailDto>.Failure(errors);
        }

        var question = await LoadAsync(id, tracking: true, ct);
        if (question is null)
        {
            return QuestionNotFound;
        }

        if (!db.TryApplyRowVersion(question, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        if (request.CategoryId != question.CategoryId && await ValidateCategoryAsync(request.CategoryId, ct) is { } categoryError)
        {
            return categoryError;
        }

        var old = new { question.QuestionType, question.Content };
        question.Update(ToData(request), currentUser.RequiredUserId, Now);
        audit.Write(AuditActions.QuestionUpdated, nameof(Question), question.Id, old, new { question.QuestionType, question.Content });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(question, ct);
    }

    public async Task<Result<QuestionDetailDto>> SetActiveAsync(Guid id, SetActiveRequest request, CancellationToken ct)
    {
        var question = await LoadAsync(id, tracking: true, ct);
        if (question is null)
        {
            return QuestionNotFound;
        }

        if (question.IsActive != request.IsActive)
        {
            question.SetActive(request.IsActive, currentUser.RequiredUserId, Now);
            audit.Write(
                AuditActions.QuestionStatusChanged, nameof(Question), question.Id, new { IsActive = !request.IsActive }, new { request.IsActive });
            await db.SaveChangesAsync(ct);
        }

        return await ToDetailAsync(question, ct);
    }

    public async Task<Result<QuestionDetailDto>> CloneAsync(Guid id, CancellationToken ct)
    {
        var source = await LoadAsync(id, tracking: false, ct);
        if (source is null)
        {
            return QuestionNotFound;
        }

        var clone = source.Clone(await codeGenerator.NextQuestionCodeAsync(ct), currentUser.RequiredUserId, Now);
        db.Questions.Add(clone);
        audit.Write(AuditActions.QuestionCreated, nameof(Question), clone.Id, newValue: new { clone.Code, ClonedFrom = source.Code });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(clone, ct);
    }

    /// <summary>Chuẩn hóa input: mã option mặc định A, B, C…; nhãn Đúng/Sai cho TRUE_FALSE.</summary>
    internal static QuestionData ToData(QuestionInput input)
    {
        var options = new List<OptionData>();
        if (input.QuestionType is QuestionType.SingleChoice or QuestionType.MultipleChoice && input.Options is not null)
        {
            var used = input.Options.Where(o => !string.IsNullOrWhiteSpace(o.OptionCode))
                .Select(o => o.OptionCode!.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal);
            var free = new Queue<string>(ChoiceCodes.Where(c => !used.Contains(c)));
            options.AddRange(input.Options.Select(o => new OptionData(
                string.IsNullOrWhiteSpace(o.OptionCode) ? free.Dequeue() : o.OptionCode.Trim().ToUpperInvariant(),
                o.Content,
                o.IsCorrect)));
        }
        else if (input.QuestionType == QuestionType.TrueFalse && input.Options is not null)
        {
            options.AddRange(input.Options
                .Select(o => (Code: o.OptionCode!.Trim().ToUpperInvariant(), o.Content, o.IsCorrect))
                .OrderBy(o => o.Code == Question.TrueCode ? 0 : 1)
                .Select(o => new OptionData(
                    o.Code,
                    string.IsNullOrWhiteSpace(o.Content) ? (o.Code == Question.TrueCode ? "Đúng" : "Sai") : o.Content,
                    o.IsCorrect)));
        }

        return new QuestionData(
            input.CategoryId,
            input.Content,
            input.ContentFormat,
            input.QuestionType,
            input.QuestionType == QuestionType.FillIn ? input.AnswerDataType : null,
            input.CorrectAnswerNumber,
            input.AnswerDataType == AnswerDataType.Number ? input.NumericTolerance ?? 0 : null,
            input.CaseSensitive,
            input.IgnoreAccent,
            input.Explanation,
            input.DefaultScore,
            options,
            input.QuestionType == QuestionType.FillIn ? input.AcceptedAnswers?.ToList() ?? [] : [],
            input.Difficulty,
            input.Tags?.ToList() ?? [],
            input.QuestionType == QuestionType.MultipleChoice && input.PartialScoring);
    }

    private async Task<Error?> ValidateCategoryAsync(Guid? categoryId, CancellationToken ct) =>
        categoryId is { } id && !await db.QuestionCategories.AnyAsync(c => c.Id == id && c.IsActive, ct)
            ? Error.Validation("CATEGORY_INVALID", "Danh mục không tồn tại hoặc đã bị tắt.", "categoryId")
            : null;

    private Task<Question?> LoadAsync(Guid id, bool tracking, CancellationToken ct)
    {
        IQueryable<Question> query = db.Questions.Include(q => q.Options).Include(q => q.AcceptedAnswers).Include(q => q.Tags);
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(q => q.Id == id, ct);
    }

    private async Task<QuestionDetailDto> ToDetailAsync(Question q, CancellationToken ct)
    {
        var categoryName = q.CategoryId is null
            ? null
            : await db.QuestionCategories.Where(c => c.Id == q.CategoryId).Select(c => c.Name).SingleOrDefaultAsync(ct);

        return new QuestionDetailDto(
            q.Id,
            q.Code,
            q.CategoryId,
            categoryName,
            q.Content,
            q.ContentFormat,
            q.QuestionType,
            q.AnswerDataType,
            q.CorrectAnswerNumber,
            q.NumericTolerance,
            q.CaseSensitive,
            q.IgnoreAccent,
            q.Explanation,
            q.DefaultScore,
            q.IsActive,
            q.Options.OrderBy(o => o.DisplayOrder)
                .Select(o => new QuestionOptionDto(o.Id, o.OptionCode, o.Content, o.IsCorrect, o.DisplayOrder)).ToList(),
            q.AcceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList(),
            q.CreatedAt,
            q.UpdatedAt,
            q.RowVersion.ToBase64(),
            q.Difficulty,
            q.Tags.Select(t => t.Tag).Order(StringComparer.Ordinal).ToList(),
            q.PartialScoring,
            mediaLinks.For([q.Content, q.Explanation, .. q.Options.Select(o => o.Content)]));
    }

    /// <summary>Các trường Markdown có thể chứa ảnh <c>media:&lt;id&gt;</c> (D-27).</summary>
    internal static IEnumerable<string?> MediaTexts(QuestionInput input) =>
        [input.Content, input.Explanation, .. (input.Options ?? []).Select(o => o.Content)];
}
