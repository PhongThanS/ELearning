using ELearning.Application.Audit;
using ELearning.Application.Common;
using ELearning.Application.Common.Abstractions;
using ELearning.Application.Media;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Questions;
using ELearning.Shared;
using ELearning.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ELearning.Application.Exams;

public interface IExamVersionService
{
    Task<Result<IReadOnlyList<VersionSummaryDto>>> ListAsync(Guid examId, CancellationToken ct);

    Task<Result<VersionDetailDto>> GetAsync(Guid examId, Guid versionId, CancellationToken ct);

    Task<Result<VersionDetailDto>> CreateAsync(Guid examId, CreateVersionRequest request, CancellationToken ct);

    Task<Result<VersionDetailDto>> UpdateAsync(Guid examId, Guid versionId, UpdateVersionRequest request, CancellationToken ct);

    Task<Result> DeleteAsync(Guid examId, Guid versionId, CancellationToken ct);

    Task<Result<VersionDetailDto>> AddQuestionsAsync(Guid examId, Guid versionId, AddVersionQuestionsRequest request, CancellationToken ct);

    Task<Result<VersionDetailDto>> UpdateQuestionAsync(
        Guid examId, Guid versionId, Guid examQuestionId, UpdateVersionQuestionRequest request, CancellationToken ct);

    Task<Result<VersionDetailDto>> RemoveQuestionAsync(Guid examId, Guid versionId, Guid examQuestionId, CancellationToken ct);

    Task<Result<VersionDetailDto>> ReorderAsync(Guid examId, Guid versionId, ReorderQuestionsRequest request, CancellationToken ct);

    Task<Result<VersionDetailDto>> SyncAsync(Guid examId, Guid versionId, SyncQuestionsRequest request, CancellationToken ct);

    Task<Result<ExamPreviewDto>> PreviewAsync(Guid examId, Guid versionId, CancellationToken ct);

    Task<Result<ExamPreviewDto>> PreviewPublishedAsync(Guid examId, CancellationToken ct);

    Task<Result<VersionDetailDto>> AddPoolRuleAsync(Guid examId, Guid versionId, AddPoolRuleRequest request, CancellationToken ct);

    Task<Result<VersionDetailDto>> RefreshPoolRuleAsync(Guid examId, Guid versionId, Guid poolRuleId, CancellationToken ct);

    Task<Result<VersionDetailDto>> RemovePoolRuleAsync(Guid examId, Guid versionId, Guid poolRuleId, CancellationToken ct);

    Task<Result<MatrixStatsDto>> GetMatrixStatsAsync(Guid examId, Guid versionId, MatrixStatsQuery query, CancellationToken ct);

    Task<Result<VersionDetailDto>> MatrixDrawAsync(Guid examId, Guid versionId, MatrixDrawRequest request, CancellationToken ct);

    Task<Result<PublishValidationDto>> ValidateAsync(Guid examId, Guid versionId, CancellationToken ct);

    Task<Result<VersionDetailDto>> PublishAsync(Guid examId, Guid versionId, CancellationToken ct);
}

/// <summary>Phiên bản đề và snapshot câu hỏi (docs/02-nghiep-vu.md mục 4.3–4.5, docs/05-api.md mục 6.4–6.5).</summary>
internal sealed class ExamVersionService(
    IAppDbContext db,
    IAuditService audit,
    ICurrentUser currentUser,
    TimeProvider time,
    MediaLinks mediaLinks,
    IValidator<UpdateVersionRequest> updateValidator,
    IValidator<AddVersionQuestionsRequest> addQuestionsValidator,
    IValidator<MatrixDrawRequest> matrixDrawValidator) : IExamVersionService
{
    private static readonly Error VersionNotFound = Error.NotFound(message: "Không tìm thấy phiên bản đề.");

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public async Task<Result<IReadOnlyList<VersionSummaryDto>>> ListAsync(Guid examId, CancellationToken ct)
    {
        if (!await db.Exams.AnyAsync(e => e.Id == examId, ct))
        {
            return ExamService.ExamNotFound;
        }

        var versions = await db.ExamVersions.AsNoTracking().Where(v => v.ExamId == examId)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new VersionSummaryDto(
                v.Id, v.VersionNumber, v.Status,
                v.Questions.Count(q => q.PoolRuleId == null) + (v.PoolRules.Sum(r => (int?)r.DrawCount) ?? 0),
                (v.Questions.Where(q => q.PoolRuleId == null).Sum(q => (decimal?)q.Score) ?? 0) + (v.PoolRules.Sum(r => (decimal?)(r.DrawCount * r.ScorePerQuestion)) ?? 0),
                v.PublishedAt, v.ArchivedAt, v.CreatedAt))
            .ToListAsync(ct);
        return versions;
    }

    public async Task<Result<VersionDetailDto>> GetAsync(Guid examId, Guid versionId, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: false, ct);
        return version is null ? VersionNotFound : await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> CreateAsync(Guid examId, CreateVersionRequest request, CancellationToken ct)
    {
        var exam = await db.Exams.Include(e => e.Versions).SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return ExamService.ExamNotFound;
        }

        if (exam.Versions.Any(v => v.IsDraft))
        {
            return Error.Conflict(ErrorCodes.DraftVersionExists, "Đề đã có một phiên bản nháp.");
        }

        var sourceId = request.CopyFromVersionId
            ?? exam.Versions.SingleOrDefault(v => v.Status == ExamVersionStatus.Published)?.Id
            ?? exam.Versions.OrderByDescending(v => v.VersionNumber).First().Id;
        var source = await LoadVersionAsync(examId, sourceId, tracking: false, ct);
        if (source is null)
        {
            return VersionNotFound;
        }

        var draft = source.CopyAsDraft(examId, exam.Versions.Max(v => v.VersionNumber) + 1, currentUser.RequiredUserId, Now);
        db.ExamVersions.Add(draft);
        audit.Write(
            AuditActions.ExamVersionCreated, nameof(ExamVersion), draft.Id,
            newValue: new { ExamId = examId, draft.VersionNumber, CopiedFrom = source.VersionNumber });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(draft, ct);
    }

    public async Task<Result<VersionDetailDto>> UpdateAsync(Guid examId, Guid versionId, UpdateVersionRequest request, CancellationToken ct)
    {
        var errors = await updateValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<VersionDetailDto>.Failure(errors);
        }

        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        if (!db.TryApplyRowVersion(version, request.RowVersion))
        {
            return RowVersionExtensions.MissingRowVersion();
        }

        var old = version.Settings;
        version.UpdateSettings(new VersionSettings(
            request.DurationMinutes, request.PassPercentage, request.ScoreVisibility, request.ReviewPolicy, request.ShuffleQuestions, request.ShuffleOptions));
        audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, old, version.Settings);
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(version, ct);
    }

    public async Task<Result> DeleteAsync(Guid examId, Guid versionId, CancellationToken ct)
    {
        var exam = await db.Exams.SingleOrDefaultAsync(e => e.Id == examId, ct);
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (exam is null || version is null)
        {
            return VersionNotFound;
        }

        version.EnsureDraft();
        if (exam.Status == ExamStatus.Draft)
        {
            return Error.Conflict(ErrorCodes.ExamNotDraft, "Đề chưa publish chỉ có một phiên bản; hãy xóa đề thay vì xóa phiên bản.");
        }

        db.ExamVersions.Remove(version);
        audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, new { Deleted = version.VersionNumber });
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<VersionDetailDto>> AddQuestionsAsync(
        Guid examId, Guid versionId, AddVersionQuestionsRequest request, CancellationToken ct)
    {
        var errors = await addQuestionsValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<VersionDetailDto>.Failure(errors);
        }

        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        version.EnsureDraft();
        var ids = request.QuestionIds.Distinct().ToList();
        var questions = await db.Questions.AsNoTracking()
            .Include(q => q.Options).Include(q => q.AcceptedAnswers)
            .Where(q => ids.Contains(q.Id))
            .ToListAsync(ct);
        if (questions.Count != ids.Count)
        {
            return Error.Validation(ErrorCodes.QuestionNotFound, "Có câu hỏi không tồn tại.", "questionIds");
        }

        if (questions.Exists(q => !q.IsActive))
        {
            return Error.Validation("QUESTION_INACTIVE", "Không thêm được câu hỏi đã bị tắt.", "questionIds");
        }

        // Giữ đúng thứ tự admin chọn
        var added = 0;
        foreach (var question in ids.Select(id => questions.First(q => q.Id == id)))
        {
            if (version.AddQuestion(question, request.Score, Now) is not null)
            {
                added++;
            }
        }

        if (added > 0)
        {
            audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new { AddedQuestionIds = ids });
            await db.SaveChangesAsync(ct);
        }

        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> UpdateQuestionAsync(
        Guid examId, Guid versionId, Guid examQuestionId, UpdateVersionQuestionRequest request, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        version.SetQuestionScore(examQuestionId, request.Score);
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> RemoveQuestionAsync(Guid examId, Guid versionId, Guid examQuestionId, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        if (!version.RemoveQuestion(examQuestionId))
        {
            return Error.NotFound(ErrorCodes.QuestionNotFound, "Câu hỏi không thuộc phiên bản này.");
        }

        audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new { RemovedExamQuestionId = examQuestionId });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> ReorderAsync(
        Guid examId, Guid versionId, ReorderQuestionsRequest request, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        version.EnsureDraft();
        var fixedQuestions = version.FixedQuestions.ToList();
        if (request.ExamQuestionIds.Count != fixedQuestions.Count
            || request.ExamQuestionIds.Distinct().Count() != fixedQuestions.Count
            || !request.ExamQuestionIds.All(id => fixedQuestions.Exists(q => q.Id == id)))
        {
            return Error.Validation("ORDER_INVALID", "Danh sách sắp xếp phải chứa đúng và đủ các câu hỏi của phiên bản.", "examQuestionIds");
        }

        // Hai bước trong một transaction để không vi phạm UQ_ExamQuestions_Order (docs/10-bay-ky-thuat.md mục 8)
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            version.ShiftOrdersForReorder();
            await db.SaveChangesAsync(ct);
            version.Reorder(request.ExamQuestionIds);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> SyncAsync(Guid examId, Guid versionId, SyncQuestionsRequest request, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        version.EnsureDraft();
        var targets = version.Questions
            .Where(q => q.SourceQuestionId is not null && (request.ExamQuestionIds is null || request.ExamQuestionIds.Contains(q.Id)))
            .ToList();
        var sourceIds = targets.Select(q => q.SourceQuestionId!.Value).ToList();
        var sources = await db.Questions.AsNoTracking()
            .Include(q => q.Options).Include(q => q.AcceptedAnswers)
            .Where(q => sourceIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);

        foreach (var target in targets.Where(t => sources.ContainsKey(t.SourceQuestionId!.Value)))
        {
            version.SyncQuestion(target.Id, sources[target.SourceQuestionId!.Value], Now);
        }

        audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new { Synced = targets.Select(t => t.Id) });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> AddPoolRuleAsync(
        Guid examId, Guid versionId, AddPoolRuleRequest request, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        version.EnsureDraft();
        if (request.Tag is { Length: > Question.MaxTagLength })
        {
            return Error.Validation("TAG_TOO_LONG", $"Tag tối đa {Question.MaxTagLength} ký tự.", "tag");
        }

        var criteria = new PoolRuleCriteria(request.CategoryId, request.Difficulty, request.Tag, request.QuestionType);
        var candidates = await LoadPoolCandidatesAsync(version, criteria, excludeRuleId: null, ct);
        if (candidates.IsFailure)
        {
            return Result<VersionDetailDto>.Failure(candidates.Errors);
        }

        var rule = version.AddPoolRule(criteria, request.DrawCount, request.ScorePerQuestion, candidates.Value, Now);
        audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new
        {
            PoolRuleAdded = rule.Id,
            criteria,
            request.DrawCount,
            request.ScorePerQuestion,
            Candidates = candidates.Value.Count,
        });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> RefreshPoolRuleAsync(Guid examId, Guid versionId, Guid poolRuleId, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        var rule = version?.PoolRules.FirstOrDefault(r => r.Id == poolRuleId);
        if (version is null || rule is null)
        {
            return VersionNotFound;
        }

        version.EnsureDraft();
        var candidates = await LoadPoolCandidatesAsync(version, rule.Criteria, rule.Id, ct);
        if (candidates.IsFailure)
        {
            return Result<VersionDetailDto>.Failure(candidates.Errors);
        }

        version.RefreshPoolRule(rule.Id, candidates.Value, Now);
        audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new { PoolRuleRefreshed = rule.Id, Candidates = candidates.Value.Count });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<VersionDetailDto>> RemovePoolRuleAsync(Guid examId, Guid versionId, Guid poolRuleId, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null || version.PoolRules.All(r => r.Id != poolRuleId))
        {
            return VersionNotFound;
        }

        version.RemovePoolRule(poolRuleId);
        audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new { PoolRuleRemoved = poolRuleId });
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(version, ct);
    }

    public async Task<Result<MatrixStatsDto>> GetMatrixStatsAsync(
        Guid examId, Guid versionId, MatrixStatsQuery query, CancellationToken ct)
    {
        var version = await LoadVersionAsync(examId, versionId, tracking: false, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        var existingSourceIds = version.Questions
            .Where(q => q.SourceQuestionId.HasValue)
            .Select(q => q.SourceQuestionId!.Value)
            .ToHashSet();

        var baseQuery = db.Questions.AsNoTracking()
            .Where(q => q.IsActive && !existingSourceIds.Contains(q.Id));

        if (query.CategoryId.HasValue)
        {
            baseQuery = baseQuery.Where(q => q.CategoryId == query.CategoryId.Value);
        }

        if (query.QuestionType.HasValue)
        {
            baseQuery = baseQuery.Where(q => q.QuestionType == query.QuestionType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = Question.NormalizeTag(query.Tag);
            baseQuery = baseQuery.Where(q => q.Tags.Any(t => t.Tag == tag));
        }

        var stats = await baseQuery
            .GroupBy(q => q.Difficulty)
            .Select(g => new { Difficulty = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var easy = stats.FirstOrDefault(s => s.Difficulty == QuestionDifficulty.Easy)?.Count ?? 0;
        var medium = stats.FirstOrDefault(s => s.Difficulty == QuestionDifficulty.Medium)?.Count ?? 0;
        var hard = stats.FirstOrDefault(s => s.Difficulty == QuestionDifficulty.Hard)?.Count ?? 0;
        var unspecified = stats.FirstOrDefault(s => s.Difficulty == null)?.Count ?? 0;
        var total = easy + medium + hard + unspecified;

        var categoryGrouped = await baseQuery
            .GroupBy(q => new { q.CategoryId, q.Difficulty })
            .Select(g => new { g.Key.CategoryId, g.Key.Difficulty, Count = g.Count() })
            .ToListAsync(ct);

        var byCategory = categoryGrouped
            .GroupBy(x => x.CategoryId)
            .Select(cg =>
            {
                var catEasy = cg.FirstOrDefault(x => x.Difficulty == QuestionDifficulty.Easy)?.Count ?? 0;
                var catMedium = cg.FirstOrDefault(x => x.Difficulty == QuestionDifficulty.Medium)?.Count ?? 0;
                var catHard = cg.FirstOrDefault(x => x.Difficulty == QuestionDifficulty.Hard)?.Count ?? 0;
                var catUnspecified = cg.FirstOrDefault(x => x.Difficulty == null)?.Count ?? 0;
                var catTotal = catEasy + catMedium + catHard + catUnspecified;
                return new CategoryMatrixStatsDto(cg.Key, catTotal, catEasy, catMedium, catHard, catUnspecified);
            })
            .ToList();

        return Result<MatrixStatsDto>.Success(new MatrixStatsDto(total, easy, medium, hard, unspecified, byCategory));
    }

    public async Task<Result<VersionDetailDto>> MatrixDrawAsync(
        Guid examId, Guid versionId, MatrixDrawRequest request, CancellationToken ct)
    {
        var errors = await matrixDrawValidator.ValidateToErrorsAsync(request, ct);
        if (errors.Count > 0)
        {
            return Result<VersionDetailDto>.Failure(errors);
        }

        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (version is null)
        {
            return VersionNotFound;
        }

        version.EnsureDraft();

        var topics = request.Topics != null && request.Topics.Count > 0
            ? request.Topics.Where(t => t.EasyCount > 0 || t.MediumCount > 0 || t.HardCount > 0).ToList()
            : [new MatrixTopicItem(request.CategoryId, request.EasyCount, request.MediumCount, request.HardCount, request.ScorePerQuestion)];

        var totalRequested = topics.Sum(t => t.EasyCount + t.MediumCount + t.HardCount);
        if (totalRequested <= 0)
        {
            return Error.Validation("TOTAL_COUNT_ZERO", "Vui lòng chọn ít nhất 1 câu hỏi.", "totalCount");
        }

        if (version.EffectiveQuestionCount + totalRequested > ExamVersion.MaxQuestions)
        {
            return Error.Validation(
                "TOO_MANY_QUESTIONS",
                $"Thêm {totalRequested} câu sẽ vượt quá giới hạn tối đa {ExamVersion.MaxQuestions} câu của đề thi (hiện có {version.EffectiveQuestionCount} câu).",
                "totalCount");
        }

        if (request.Mode == MatrixDrawMode.Static)
        {
            var existingSourceIds = version.Questions
                .Where(q => q.SourceQuestionId.HasValue)
                .Select(q => q.SourceQuestionId!.Value)
                .ToHashSet();

            var baseQuery = db.Questions.AsNoTracking()
                .Where(q => q.IsActive && !existingSourceIds.Contains(q.Id));

            if (request.QuestionType.HasValue)
            {
                baseQuery = baseQuery.Where(q => q.QuestionType == request.QuestionType.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Tag))
            {
                var tag = Question.NormalizeTag(request.Tag);
                baseQuery = baseQuery.Where(q => q.Tags.Any(t => t.Tag == tag));
            }

            var categoryIds = topics.Where(t => t.CategoryId.HasValue).Select(t => t.CategoryId!.Value).Distinct().ToList();
            var categoryNames = categoryIds.Count > 0
                ? await db.QuestionCategories.AsNoTracking()
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.Name, ct)
                : new Dictionary<Guid, string>();

            var chosen = new List<Question>();
            var chosenIds = new HashSet<Guid>();

            foreach (var topic in topics)
            {
                var topicQuery = baseQuery.Where(q => !chosenIds.Contains(q.Id));
                if (topic.CategoryId.HasValue)
                {
                    topicQuery = topicQuery.Where(q => q.CategoryId == topic.CategoryId.Value);
                }

                var topicName = topic.CategoryId.HasValue && categoryNames.TryGetValue(topic.CategoryId.Value, out var cName)
                    ? $"Chuyên đề \"{cName}\""
                    : "Ngân hàng câu hỏi";

                var easyCandidates = topic.EasyCount > 0
                    ? await topicQuery.Where(q => q.Difficulty == QuestionDifficulty.Easy)
                        .Include(q => q.Options).Include(q => q.AcceptedAnswers)
                        .ToListAsync(ct)
                    : [];

                var mediumCandidates = topic.MediumCount > 0
                    ? await topicQuery.Where(q => q.Difficulty == QuestionDifficulty.Medium)
                        .Include(q => q.Options).Include(q => q.AcceptedAnswers)
                        .ToListAsync(ct)
                    : [];

                var hardCandidates = topic.HardCount > 0
                    ? await topicQuery.Where(q => q.Difficulty == QuestionDifficulty.Hard)
                        .Include(q => q.Options).Include(q => q.AcceptedAnswers)
                        .ToListAsync(ct)
                    : [];

                if (easyCandidates.Count < topic.EasyCount)
                {
                    return Error.Validation(
                        "INSUFFICIENT_EASY_QUESTIONS",
                        $"{topicName} chỉ còn {easyCandidates.Count} câu Dễ khả dụng, không đủ {topic.EasyCount} câu yêu cầu.",
                        "easyCount");
                }

                if (mediumCandidates.Count < topic.MediumCount)
                {
                    return Error.Validation(
                        "INSUFFICIENT_MEDIUM_QUESTIONS",
                        $"{topicName} chỉ còn {mediumCandidates.Count} câu Trung bình khả dụng, không đủ {topic.MediumCount} câu yêu cầu.",
                        "mediumCount");
                }

                if (hardCandidates.Count < topic.HardCount)
                {
                    return Error.Validation(
                        "INSUFFICIENT_HARD_QUESTIONS",
                        $"{topicName} chỉ còn {hardCandidates.Count} câu Khó khả dụng, không đủ {topic.HardCount} câu yêu cầu.",
                        "hardCount");
                }

                var score = topic.ScorePerQuestion ?? request.ScorePerQuestion;

                if (topic.EasyCount > 0)
                {
                    var arr = easyCandidates.ToArray();
                    Random.Shared.Shuffle(arr);
                    var picked = arr.Take(topic.EasyCount).ToList();
                    chosen.AddRange(picked);
                    foreach (var q in picked)
                    {
                        chosenIds.Add(q.Id);
                        version.AddQuestion(q, score, Now);
                    }
                }
                if (topic.MediumCount > 0)
                {
                    var arr = mediumCandidates.ToArray();
                    Random.Shared.Shuffle(arr);
                    var picked = arr.Take(topic.MediumCount).ToList();
                    chosen.AddRange(picked);
                    foreach (var q in picked)
                    {
                        chosenIds.Add(q.Id);
                        version.AddQuestion(q, score, Now);
                    }
                }
                if (topic.HardCount > 0)
                {
                    var arr = hardCandidates.ToArray();
                    Random.Shared.Shuffle(arr);
                    var picked = arr.Take(topic.HardCount).ToList();
                    chosen.AddRange(picked);
                    foreach (var q in picked)
                    {
                        chosenIds.Add(q.Id);
                        version.AddQuestion(q, score, Now);
                    }
                }
            }

            audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new
            {
                MatrixDrawMode = "STATIC",
                request.CategoryId,
                request.EasyCount,
                request.MediumCount,
                request.HardCount,
                request.ScorePerQuestion,
                TopicCount = topics.Count,
                AddedCount = chosen.Count,
                QuestionIds = chosen.Select(q => q.Id).ToList()
            });

            await db.SaveChangesAsync(ct);
            return await ToDetailAsync(version, ct);
        }
        else // MatrixDrawMode.Pool
        {
            var addedRuleIds = new List<Guid>();

            foreach (var topic in topics)
            {
                var diffItems = new (QuestionDifficulty Difficulty, int Count)[]
                {
                    (QuestionDifficulty.Easy, topic.EasyCount),
                    (QuestionDifficulty.Medium, topic.MediumCount),
                    (QuestionDifficulty.Hard, topic.HardCount)
                }.Where(x => x.Count > 0).ToList();

                var score = topic.ScorePerQuestion ?? request.ScorePerQuestion ?? 1m;

                foreach (var item in diffItems)
                {
                    var criteria = new PoolRuleCriteria(topic.CategoryId, item.Difficulty, request.Tag, request.QuestionType);
                    var candidates = await LoadPoolCandidatesAsync(version, criteria, excludeRuleId: null, ct);
                    if (candidates.IsFailure)
                    {
                        return Result<VersionDetailDto>.Failure(candidates.Errors);
                    }

                    if (candidates.Value.Count < item.Count)
                    {
                        return Error.Validation(
                            "INSUFFICIENT_POOL_CANDIDATES",
                            $"Số câu ứng viên ({candidates.Value.Count}) cho mức độ {item.Difficulty} không đủ số câu bốc {item.Count}.",
                            "drawCount");
                    }

                    var rule = version.AddPoolRule(criteria, item.Count, score, candidates.Value, Now);
                    addedRuleIds.Add(rule.Id);
                }
            }

            audit.Write(AuditActions.ExamUpdated, nameof(ExamVersion), version.Id, newValue: new
            {
                MatrixDrawMode = "POOL",
                request.CategoryId,
                request.EasyCount,
                request.MediumCount,
                request.HardCount,
                TopicCount = topics.Count,
                PoolRuleIds = addedRuleIds
            });

            await db.SaveChangesAsync(ct);
            return await ToDetailAsync(version, ct);
        }
    }

    /// <summary>
    /// Câu ứng viên: đang hoạt động, khớp mọi tiêu chí, chưa có trong version (trừ câu của chính quy tắc đang làm mới).
    /// Đếm trước để không nạp cả ngân hàng khi tiêu chí quá rộng.
    /// </summary>
    private async Task<Result<IReadOnlyList<Question>>> LoadPoolCandidatesAsync(
        ExamVersion version, PoolRuleCriteria criteria, Guid? excludeRuleId, CancellationToken ct)
    {
        var existing = version.Questions
            .Where(q => q.SourceQuestionId != null && (excludeRuleId == null || q.PoolRuleId != excludeRuleId))
            .Select(q => q.SourceQuestionId!.Value)
            .ToList();
        var query = db.Questions.AsNoTracking().Where(q => q.IsActive && !existing.Contains(q.Id));
        if (criteria.CategoryId is { } categoryId)
        {
            query = query.Where(q => q.CategoryId == categoryId);
        }

        if (criteria.Difficulty is { } difficulty)
        {
            query = query.Where(q => q.Difficulty == difficulty);
        }

        if (criteria.QuestionType is { } type)
        {
            query = query.Where(q => q.QuestionType == type);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Tag))
        {
            var tag = Question.NormalizeTag(criteria.Tag);
            query = query.Where(q => q.Tags.Any(t => t.Tag == tag));
        }

        var count = await query.CountAsync(ct);
        if (count > ExamPoolRule.MaxCandidates)
        {
            return Error.Validation(
                "POOL_TOO_LARGE",
                $"Tiêu chí khớp {count} câu, vượt quá {ExamPoolRule.MaxCandidates}. Hãy lọc thêm theo danh mục, độ khó, tag hoặc loại câu.",
                "criteria");
        }

        var candidates = await query.Include(q => q.Options).Include(q => q.AcceptedAnswers)
            .OrderBy(q => q.Code)
            .ToListAsync(ct);
        return Result<IReadOnlyList<Question>>.Success(candidates);
    }

    public async Task<Result<ExamPreviewDto>> PreviewAsync(Guid examId, Guid versionId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct);
        var version = await LoadVersionAsync(examId, versionId, tracking: false, ct);
        if (exam is null || version is null)
        {
            return VersionNotFound;
        }

        // Xem trước như học viên: pool được bốc thử một lần (mỗi lần xem trước một bộ khác)
        var questions = version.DrawQuestions(Random.Shared, allowPartial: version.IsDraft)
            .Select((q, index) => ToPlayerQuestion(q, q.Id, index + 1, optionOrder: null))
            .ToList();
        return new ExamPreviewDto(
            exam.Id,
            version.Id,
            exam.Name,
            exam.Instructions,
            version.DurationMinutes,
            questions.Count,
            version.EffectiveMaxScore,
            questions,
            mediaLinks.For(questions.SelectMany(MediaTexts)));
    }

    public async Task<Result<ExamPreviewDto>> PreviewPublishedAsync(Guid examId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null)
        {
            return VersionNotFound;
        }

        var version = await db.ExamVersions.AsNoTracking()
            .Where(v => v.ExamId == examId && v.Status == ExamVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        version ??= await db.ExamVersions.AsNoTracking()
            .Where(v => v.ExamId == examId)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        if (version is null)
        {
            return VersionNotFound;
        }

        return await PreviewAsync(examId, version.Id, ct);
    }

    /// <summary>Nội dung học viên nhìn thấy của một câu: đề và lựa chọn (không có giải thích), cho bảng ảnh (D-27).</summary>
    internal static IEnumerable<string?> MediaTexts(PlayerQuestionDto q) => [q.Content, .. q.Options.Select(o => o.Content)];

    public async Task<Result<PublishValidationDto>> ValidateAsync(Guid examId, Guid versionId, CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().Include(e => e.Assignments).SingleOrDefaultAsync(e => e.Id == examId, ct);
        var version = await LoadVersionAsync(examId, versionId, tracking: false, ct);
        if (exam is null || version is null)
        {
            return VersionNotFound;
        }

        var issues = version.ValidateForPublish(exam, exam.Assignments.Count > 0, Now);
        return new PublishValidationDto(issues.Count == 0, issues);
    }

    public async Task<Result<VersionDetailDto>> PublishAsync(Guid examId, Guid versionId, CancellationToken ct)
    {
        var exam = await db.Exams.Include(e => e.Assignments).Include(e => e.Versions).SingleOrDefaultAsync(e => e.Id == examId, ct);
        var version = await LoadVersionAsync(examId, versionId, tracking: true, ct);
        if (exam is null || version is null)
        {
            return VersionNotFound;
        }

        var issues = version.ValidateForPublish(exam, exam.Assignments.Count > 0, Now);
        if (issues.Count > 0)
        {
            var errors = new List<Error>
            {
                Error.Business(ErrorCodes.PublishValidationFailed, $"Không thể publish đề thi: có {issues.Count} lỗi."),
            };
            errors.AddRange(issues.Select(i => new Error(ErrorType.BusinessRule, i.Code, i.Message, i.Field)));
            return Result<VersionDetailDto>.Failure(errors);
        }

        // Một transaction: archive version cũ → publish version mới → audit (docs/02-nghiep-vu.md mục 4.5).
        // RowVersion của Exam / ExamVersion chặn hai admin publish cùng lúc (→ 409).
        var previous = exam.Versions.SingleOrDefault(v => v.Status == ExamVersionStatus.Published);
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            if (previous is not null)
            {
                // Archive và lưu trước, để UX_ExamVersions_OnePublished không bị vi phạm giữa chừng.
                previous.Archive(Now);
                await db.SaveChangesAsync(ct);
            }

            version.Publish(currentUser.RequiredUserId, Now);
            exam.MarkPublished(currentUser.RequiredUserId, Now);
            audit.Write(
                AuditActions.ExamPublished, nameof(ExamVersion), version.Id,
                previous is null ? null : new { ArchivedVersion = previous.VersionNumber },
                new { ExamId = examId, version.VersionNumber, version.QuestionCount, version.MaxScore });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return await ToDetailAsync(version, ct);
    }

    /// <summary>Câu hỏi dạng học viên thấy; optionOrder = thứ tự option riêng của lượt thi (null = mặc định).</summary>
    internal static PlayerQuestionDto ToPlayerQuestion(ExamQuestion q, Guid id, int order, string? optionOrder)
    {
        var options = q.Options.OrderBy(o => o.DisplayOrder).ToList();
        if (!string.IsNullOrEmpty(optionOrder))
        {
            var codes = optionOrder.Split(',');
            options = [.. options.OrderBy(o => Array.IndexOf(codes, o.OptionCode))];
        }

        int? expectedLength = q.QuestionType == QuestionType.FillIn
            ? (q.CorrectAnswerNumber != null
                ? q.CorrectAnswerNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',').Length
                : q.AcceptedAnswers.FirstOrDefault()?.AnswerText?.Length)
            : null;

        return new PlayerQuestionDto(
            id,
            order,
            q.Content,
            q.ContentFormat,
            q.QuestionType,
            q.AnswerDataType,
            q.Score,
            options.Select(o => new PlayerOptionDto(o.OptionCode, o.Content)).ToList(),
            expectedLength);
    }

    private Task<ExamVersion?> LoadVersionAsync(Guid examId, Guid versionId, bool tracking, CancellationToken ct)
    {
        IQueryable<ExamVersion> query = db.ExamVersions
            .Include(v => v.Questions).ThenInclude(q => q.Options)
            .Include(v => v.Questions).ThenInclude(q => q.AcceptedAnswers)
            .Include(v => v.PoolRules);
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(v => v.Id == versionId && v.ExamId == examId, ct);
    }

    private async Task<VersionDetailDto> ToDetailAsync(ExamVersion version, CancellationToken ct)
    {
        var sourceIds = version.Questions.Where(q => q.SourceQuestionId != null).Select(q => q.SourceQuestionId!.Value).ToList();
        var sources = await db.Questions.AsNoTracking()
            .Where(q => sourceIds.Contains(q.Id))
            .Select(q => new { q.Id, q.Code, q.RowVersion })
            .ToDictionaryAsync(q => q.Id, ct);

        var questions = version.Questions.OrderBy(q => q.QuestionOrder).Select(q =>
        {
            var source = q.SourceQuestionId is { } sid && sources.TryGetValue(sid, out var s) ? s : null;
            var changed = version.IsDraft && source is not null
                && (q.SourceRowVersion is null || !source.RowVersion.AsSpan().SequenceEqual(q.SourceRowVersion));
            return new VersionQuestionDto(
                q.Id,
                q.QuestionOrder,
                q.SourceQuestionId,
                source?.Code,
                changed,
                q.Content,
                q.ContentFormat,
                q.QuestionType,
                q.AnswerDataType,
                q.Score,
                q.IsVoided,
                q.Options.OrderBy(o => o.DisplayOrder).Select(o => new VersionOptionDto(o.OptionCode, o.Content, o.IsCorrect, o.DisplayOrder)).ToList(),
                q.AcceptedAnswers.OrderBy(a => a.DisplayOrder).Select(a => a.AnswerText).ToList(),
                q.CorrectAnswerNumber,
                q.NumericTolerance,
                q.CaseSensitive,
                q.IgnoreAccent,
                q.Explanation,
                q.PoolRuleId);
        }).ToList();

        var categoryIds = version.PoolRules.Where(r => r.CategoryId != null).Select(r => r.CategoryId!.Value).Distinct().ToList();
        var categoryNames = await db.QuestionCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var poolRules = version.PoolRules.OrderBy(r => r.RuleOrder).Select(r => new PoolRuleDto(
                r.Id,
                r.RuleOrder,
                r.CategoryId,
                r.CategoryId is { } cid && categoryNames.TryGetValue(cid, out var name) ? name : null,
                r.Difficulty,
                r.Tag,
                r.QuestionType,
                r.DrawCount,
                r.ScorePerQuestion,
                version.PoolCandidates(r.Id).Count()))
            .ToList();

        return new VersionDetailDto(
            version.Id,
            version.ExamId,
            version.VersionNumber,
            version.Status,
            version.DurationMinutes,
            version.PassPercentage,
            version.ScoreVisibility,
            version.ReviewPolicy,
            version.ShuffleQuestions,
            version.ShuffleOptions,
            version.EffectiveQuestionCount,
            version.EffectiveMaxScore,
            version.PublishedAt,
            version.ArchivedAt,
            questions,
            version.RowVersion.ToBase64(),
            poolRules,
            mediaLinks.For(version.Questions.SelectMany(q => (IEnumerable<string?>)[q.Content, q.Explanation, .. q.Options.Select(o => o.Content)])));
    }
}
