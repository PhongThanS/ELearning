using ELearning.Application.Attempts;
using ELearning.Domain.Attempts;
using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Questions;
using ELearning.UnitTests.TestHelpers;

namespace ELearning.UnitTests.Attempts;

public class AttemptDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public void Expiry_is_start_plus_duration_capped_by_end_at()
    {
        NewAttempt(duration: 60, endAt: null).ExpiredAt.Should().Be(Now.AddMinutes(60));
        NewAttempt(duration: 60, endAt: Now.AddMinutes(12)).ExpiredAt.Should().Be(Now.AddMinutes(12));
    }

    [Fact]
    public void Grace_period_boundaries()
    {
        var attempt = NewAttempt(duration: 1);

        attempt.AcceptsAnswers(Now.AddSeconds(60 + 29), Grace).Should().BeTrue();
        attempt.AcceptsAnswers(Now.AddSeconds(60 + 30), Grace).Should().BeTrue();
        attempt.AcceptsAnswers(Now.AddSeconds(60 + 31), Grace).Should().BeFalse();
        attempt.IsExpired(Now.AddSeconds(60 + 31), Grace).Should().BeTrue();
    }

    [Fact]
    public void Late_student_submit_is_recorded_as_time_expired_at_deadline()
    {
        var attempt = NewAttempt(duration: 1);

        attempt.Submit(Now.AddMinutes(5), Grace, SubmitReason.Student, null);

        attempt.Status.Should().Be(AttemptStatus.AutoSubmitted);
        attempt.SubmitReason.Should().Be(SubmitReason.TimeExpired);
        attempt.SubmittedAt.Should().Be(attempt.ExpiredAt);
    }

    [Fact]
    public void Finished_attempt_cannot_be_submitted_or_extended_again()
    {
        var attempt = NewAttempt(duration: 30);
        attempt.Submit(Now.AddMinutes(1), Grace, SubmitReason.Student, null);

        var submit = () => attempt.Submit(Now.AddMinutes(2), Grace, SubmitReason.Student, null);
        var extend = () => attempt.Extend(5);

        submit.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.AttemptNotInProgress);
        extend.Should().Throw<DomainException>();
    }

    [Fact]
    public void Extension_moves_deadline_and_accumulates()
    {
        var attempt = NewAttempt(duration: 30);

        attempt.Extend(10);
        attempt.Extend(5);

        attempt.ExpiredAt.Should().Be(Now.AddMinutes(45));
        attempt.TimeExtensionMinutes.Should().Be(15);
        var tooLong = () => attempt.Extend(ExamAttempt.MaxExtensionMinutes + 1);
        tooLong.Should().Throw<DomainException>();
    }

    [Fact]
    public void Answer_apply_ignores_stale_client_seq()
    {
        var answer = NewAttempt(duration: 30).Questions.First().Answer;

        answer.Apply(["B"], null, null, true, 5, Now).Should().BeTrue();
        answer.Apply(["A"], null, null, false, 4, Now).Should().BeFalse();
        answer.Apply([], null, null, false, 6, Now).Should().BeTrue();

        answer.SelectedOptions.Should().BeEmpty();
        answer.IsAnswered.Should().BeFalse("gửi rỗng = xóa câu trả lời");
        answer.ClientSeq.Should().Be(6);
        answer.SaveCount.Should().Be(2);
    }

    [Theory]
    [InlineData(ScoreVisibility.Immediate, ReviewPolicy.AfterSubmit, false, 1, 1, true, true)]
    [InlineData(ScoreVisibility.Immediate, ReviewPolicy.Never, false, 1, 1, true, false)]
    [InlineData(ScoreVisibility.Hidden, ReviewPolicy.AfterSubmit, false, 1, 1, false, false)]
    [InlineData(ScoreVisibility.AfterExamEnd, ReviewPolicy.AfterExamEnd, false, 1, 1, false, false)]
    [InlineData(ScoreVisibility.AfterExamEnd, ReviewPolicy.AfterExamEnd, true, 1, 1, true, true)]
    [InlineData(ScoreVisibility.Immediate, ReviewPolicy.AfterLastAttempt, false, 1, 3, true, false)]
    [InlineData(ScoreVisibility.Immediate, ReviewPolicy.AfterLastAttempt, false, 3, 3, true, true)]
    [InlineData(ScoreVisibility.Immediate, ReviewPolicy.AfterLastAttempt, true, 1, 3, true, true)]
    public void Result_policy_matrix(
        ScoreVisibility score, ReviewPolicy review, bool ended, int used, int allowed, bool scoreVisible, bool reviewAvailable)
    {
        DateTime? endAt = ended ? Now.AddMinutes(-1) : Now.AddHours(1);

        var result = ResultPolicy.Evaluate(AttemptStatus.Submitted, score, review, endAt, used, allowed, Now);

        result.ScoreVisible.Should().Be(scoreVisible);
        result.ReviewAvailable.Should().Be(reviewAvailable);
    }

    [Fact]
    public void Result_policy_hides_everything_for_unfinished_attempts()
    {
        foreach (var status in new[] { AttemptStatus.InProgress, AttemptStatus.Cancelled })
        {
            ResultPolicy.Evaluate(status, ScoreVisibility.Immediate, ReviewPolicy.AfterSubmit, null, 1, 1, Now)
                .Should().Be(new ResultVisibility(false, false, null));
        }
    }

    [Fact]
    public void Review_available_at_points_to_end_at_when_waiting()
    {
        var endAt = Now.AddHours(2);

        ResultPolicy.Evaluate(AttemptStatus.Submitted, ScoreVisibility.Immediate, ReviewPolicy.AfterExamEnd, endAt, 1, 1, Now)
            .ReviewAvailableAt.Should().Be(endAt);
        ResultPolicy.Evaluate(AttemptStatus.Submitted, ScoreVisibility.Immediate, ReviewPolicy.Never, endAt, 1, 1, Now)
            .ReviewAvailableAt.Should().BeNull();
    }

    private static ExamAttempt NewAttempt(int duration, DateTime? endAt = null)
    {
        var exam = Exam.Create(
            "EX",
            new ExamDetails("Đề", null, null, null, endAt, 1, AccessMode.Public, RetakeScoringPolicy.Highest),
            new VersionSettings(duration, null, ScoreVisibility.Immediate, ReviewPolicy.Never),
            Admin,
            Now.AddDays(-1)).WithId();
        var version = exam.Versions.Single();
        typeof(ExamVersion).GetProperty(nameof(ExamVersion.ExamId))!.SetValue(version, exam.Id);
        var question = Question.Create(
            "Q1",
            new QuestionData(null, "Chọn B", ContentFormat.Plain, QuestionType.SingleChoice, null, null, null, false, false, null, 1m,
                [new OptionData("A", "A", false), new OptionData("B", "B", true)], []),
            Admin,
            Now).WithId();
        version.AddQuestion(question, null, Now)!.WithId();
        version.Publish(Admin, Now);
        return ExamAttempt.Start(exam, version, Guid.NewGuid(), 1, Now, null, null);
    }
}
