using System.Text.Json;
using ELearning.Application.Common.Abstractions;
using ELearning.Domain.Audit;

namespace ELearning.Application.Audit;

public interface IAuditService
{
    /// <summary>
    /// Thêm bản ghi audit vào DbContext; được lưu cùng SaveChanges (cùng transaction) với thao tác.
    /// Không truyền dữ liệu nhạy cảm (mật khẩu, token, đáp án) vào oldValue / newValue.
    /// </summary>
    void Write(
        string action,
        string? entityName = null,
        Guid? entityId = null,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        Guid? userId = null);
}

internal sealed class AuditService(IAppDbContext db, ICurrentUser currentUser, TimeProvider time) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Write(
        string action,
        string? entityName = null,
        Guid? entityId = null,
        object? oldValue = null,
        object? newValue = null,
        string? reason = null,
        Guid? userId = null)
    {
        db.AuditLogs.Add(new AuditLog(
            action,
            userId ?? currentUser.UserId,
            entityName,
            entityId,
            Serialize(oldValue),
            Serialize(newValue),
            reason,
            currentUser.IpAddress,
            Truncate(currentUser.UserAgent, 500),
            currentUser.TraceId is { Length: > 64 } t ? t[..64] : currentUser.TraceId,
            time.GetUtcNow().UtcDateTime));
    }

    private static string? Serialize(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static string? Truncate(string? value, int max) => value is { Length: > 0 } && value.Length > max ? value[..max] : value;
}

/// <summary>Danh sách action audit (docs/07-bao-mat.md mục 9).</summary>
public static class AuditActions
{
    public const string UserLogin = "USER_LOGIN";
    public const string UserLoginFailed = "USER_LOGIN_FAILED";
    public const string UserLockedOut = "USER_LOCKED_OUT";
    public const string UserLogout = "USER_LOGOUT";
    public const string UserRegister = "USER_REGISTER";
    public const string PasswordChanged = "PASSWORD_CHANGED";
    public const string PasswordResetByAdmin = "PASSWORD_RESET_BY_ADMIN";
    public const string RefreshTokenReuse = "REFRESH_TOKEN_REUSE";

    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserStatusChanged = "USER_STATUS_CHANGED";
    public const string UserRolesChanged = "USER_ROLES_CHANGED";
    public const string UserAnonymized = "USER_ANONYMIZED";

    public const string GroupCreated = "GROUP_CREATED";
    public const string GroupUpdated = "GROUP_UPDATED";
    public const string GroupDeleted = "GROUP_DELETED";
    public const string GroupMembersChanged = "GROUP_MEMBERS_CHANGED";
    public const string RoleCreated = "ROLE_CREATED";
    public const string RoleUpdated = "ROLE_UPDATED";
    public const string RolePermissionsChanged = "ROLE_PERMISSIONS_CHANGED";

    public const string CategoryCreated = "CATEGORY_CREATED";
    public const string CategoryUpdated = "CATEGORY_UPDATED";
    public const string CategoryDeleted = "CATEGORY_DELETED";
    public const string CategoryStatusChanged = "CATEGORY_STATUS_CHANGED";
    public const string QuestionCreated = "QUESTION_CREATED";
    public const string QuestionUpdated = "QUESTION_UPDATED";
    public const string QuestionStatusChanged = "QUESTION_STATUS_CHANGED";
    public const string QuestionsImported = "QUESTIONS_IMPORTED";
    public const string MediaUploaded = "MEDIA_UPLOADED";
    public const string AnswerManuallyGraded = "ANSWER_MANUALLY_GRADED";

    public const string ExamCreated = "EXAM_CREATED";
    public const string ExamUpdated = "EXAM_UPDATED";
    public const string ExamDeleted = "EXAM_DELETED";
    public const string ExamVersionCreated = "EXAM_VERSION_CREATED";
    public const string ExamPublished = "EXAM_PUBLISHED";
    public const string ExamClosed = "EXAM_CLOSED";
    public const string ExamReopened = "EXAM_REOPENED";
    public const string ExamAssignmentsChanged = "EXAM_ASSIGNMENTS_CHANGED";

    public const string AnswerKeyCorrected = "ANSWER_KEY_CORRECTED";
    public const string QuestionVoided = "QUESTION_VOIDED";
    public const string ExamRegraded = "EXAM_REGRADED";

    public const string AttemptStarted = "ATTEMPT_STARTED";
    public const string AttemptSubmitted = "ATTEMPT_SUBMITTED";
    public const string AttemptAutoSubmitted = "ATTEMPT_AUTO_SUBMITTED";
    public const string AttemptExtended = "ATTEMPT_EXTENDED";
    public const string AttemptForceSubmitted = "ATTEMPT_FORCE_SUBMITTED";
    public const string AttemptCancelled = "ATTEMPT_CANCELLED";
    public const string AttemptsGranted = "ATTEMPTS_GRANTED";

    public const string ResultCreated = "RESULT_CREATED";
    public const string ResultsExported = "RESULTS_EXPORTED";
}
