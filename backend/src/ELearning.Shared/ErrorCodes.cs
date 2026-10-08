namespace ELearning.Shared;

/// <summary>Danh mục mã lỗi dùng chung (docs/05-api.md mục 4).</summary>
public static class ErrorCodes
{
    // 400 / 405
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string InvalidValue = "INVALID_VALUE";
    public const string MediaFileRequired = "MEDIA_FILE_REQUIRED";
    public const string MediaTooLarge = "MEDIA_TOO_LARGE";
    public const string MediaTypeNotAllowed = "MEDIA_TYPE_NOT_ALLOWED";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";

    // 401
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string TokenInvalid = "TOKEN_INVALID";

    // 403
    public const string Forbidden = "FORBIDDEN";
    public const string RegistrationDisabled = "REGISTRATION_DISABLED";
    public const string PasswordChangeRequired = "PASSWORD_CHANGE_REQUIRED";
    public const string CsrfCheckFailed = "CSRF_CHECK_FAILED";

    // 404
    public const string NotFound = "NOT_FOUND";
    public const string ExamNotFound = "EXAM_NOT_FOUND";
    public const string AttemptNotFound = "ATTEMPT_NOT_FOUND";
    public const string ClassroomNotFound = "CLASSROOM_NOT_FOUND";
    public const string ClassroomInUse = "CLASSROOM_IN_USE";
    public const string QuestionNotFound = "QUESTION_NOT_FOUND";
    public const string MediaNotFound = "MEDIA_NOT_FOUND";

    // 409
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string DuplicateCode = "DUPLICATE_CODE";
    public const string AttemptNotInProgress = "ATTEMPT_NOT_IN_PROGRESS";
    public const string VersionImmutable = "VERSION_IMMUTABLE";
    public const string ExamNotDraft = "EXAM_NOT_DRAFT";
    public const string DraftVersionExists = "DRAFT_VERSION_EXISTS";
    public const string RetakePolicyLocked = "RETAKE_POLICY_LOCKED";
    public const string UserNameTaken = "USERNAME_TAKEN";
    public const string EmailTaken = "EMAIL_TAKEN";

    // 422
    public const string ExamNotAvailable = "EXAM_NOT_AVAILABLE";
    public const string MaxAttemptsExceeded = "MAX_ATTEMPTS_EXCEEDED";
    public const string AttemptExpired = "ATTEMPT_EXPIRED";
    public const string InvalidOption = "INVALID_OPTION";
    public const string InvalidAnswerShape = "INVALID_ANSWER_SHAPE";
    public const string InvalidNumberFormat = "INVALID_NUMBER_FORMAT";
    public const string PublishValidationFailed = "PUBLISH_VALIDATION_FAILED";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string ResultNotAvailable = "RESULT_NOT_AVAILABLE";
    public const string InvalidCurrentPassword = "INVALID_CURRENT_PASSWORD";
    public const string CannotModifySelf = "CANNOT_MODIFY_SELF";
    public const string UserAnonymized = "USER_ANONYMIZED";
    public const string SystemRoleImmutable = "SYSTEM_ROLE_IMMUTABLE";

    // 429 / 500
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";
}
