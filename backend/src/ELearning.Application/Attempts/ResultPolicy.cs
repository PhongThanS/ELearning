using ELearning.Domain.Enums;

namespace ELearning.Application.Attempts;

/// <summary>Học viên thấy gì với một lượt thi đã nộp.</summary>
public sealed record ResultVisibility(bool ScoreVisible, bool ReviewAvailable, DateTime? ReviewAvailableAt);

/// <summary>
/// Chính sách hiển thị điểm và xem lại bài (D-09, docs/02-nghiep-vu.md mục 7).
/// Xem lại bài luôn kèm điểm: phải thỏa cả hai chính sách.
/// </summary>
public static class ResultPolicy
{
    public static ResultVisibility Evaluate(
        AttemptStatus status,
        ScoreVisibility scoreVisibility,
        ReviewPolicy reviewPolicy,
        DateTime? examEndAt,
        int usedAttempts,
        int allowedAttempts,
        DateTime now)
    {
        if (status is not (AttemptStatus.Submitted or AttemptStatus.AutoSubmitted))
        {
            return new ResultVisibility(false, false, null);
        }

        var ended = examEndAt is { } end && end <= now;
        var scoreVisible = scoreVisibility switch
        {
            ScoreVisibility.Immediate => true,
            ScoreVisibility.AfterExamEnd => ended,
            _ => false,
        };

        var reviewAllowed = reviewPolicy switch
        {
            ReviewPolicy.AfterSubmit => true,
            ReviewPolicy.AfterExamEnd => ended,
            ReviewPolicy.AfterLastAttempt => ended || usedAttempts >= allowedAttempts,
            _ => false,
        };

        var reviewAvailable = scoreVisible && reviewAllowed;

        // Thời điểm sẽ được xem lại: chỉ có khi chính sách cho xem lại nhưng đang phải chờ EndAt.
        DateTime? reviewAt = !reviewAvailable
            && reviewPolicy != ReviewPolicy.Never
            && scoreVisibility != ScoreVisibility.Hidden
            && examEndAt is not null
            && !ended
                ? examEndAt
                : null;

        return new ResultVisibility(scoreVisible, reviewAvailable, reviewAt);
    }
}
