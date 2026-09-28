namespace ELearning.Domain.Common;

/// <summary>
/// Mã lỗi phát sinh trong domain. Các mã dùng chung với API nằm ở ELearning.Shared.ErrorCodes;
/// ở đây chỉ giữ mã riêng của domain.
/// </summary>
public static class DomainErrorCodes
{
    public const string UserAnonymized = "USER_ANONYMIZED";
    public const string InvalidStateTransition = "INVALID_STATE_TRANSITION";
    public const string SystemRoleImmutable = "SYSTEM_ROLE_IMMUTABLE";
    public const string InvalidQuestion = "INVALID_QUESTION";
    public const string InvalidExam = "INVALID_EXAM";
    public const string VersionImmutable = "VERSION_IMMUTABLE";
    public const string InvalidAttempt = "INVALID_ATTEMPT";
    public const string AttemptNotInProgress = "ATTEMPT_NOT_IN_PROGRESS";
}
