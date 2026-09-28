using ELearning.Domain.Common;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Questions;
using ELearning.UnitTests.TestHelpers;

namespace ELearning.UnitTests.Exams;

public class ExamDomainTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public void New_exam_is_draft_with_version_one()
    {
        var exam = NewExam();

        exam.Status.Should().Be(ExamStatus.Draft);
        exam.Versions.Should().ContainSingle().Which.Should().Match<ExamVersion>(v => v.VersionNumber == 1 && v.IsDraft);
    }

    [Fact]
    public void Publish_validation_lists_every_issue()
    {
        var exam = NewExam(maxAttempts: 3, accessMode: AccessMode.Assigned);
        var version = CreateVersion(exam, new VersionSettings(60, null, ScoreVisibility.AfterExamEnd, ReviewPolicy.AfterSubmit));

        var codes = version.ValidateForPublish(exam, hasAssignments: false, Now).Select(i => i.Code).ToList();

        codes.Should().Contain(["NO_QUESTIONS", "END_AT_REQUIRED", "REVIEW_AFTER_SUBMIT_WITH_RETAKES", "NO_ASSIGNMENTS"]);
    }

    [Fact]
    public void Publish_rejects_end_date_in_the_past()
    {
        var exam = NewExam(endAt: Now.AddMinutes(-1));
        var version = exam.Versions.Single();
        version.AddQuestion(NumberQuestion(), null, Now);

        version.ValidateForPublish(exam, hasAssignments: true, Now).Select(i => i.Code).Should().Contain("END_AT_IN_PAST");
    }

    [Fact]
    public void Valid_version_publishes_and_becomes_immutable()
    {
        var exam = NewExam();
        var version = exam.Versions.Single();
        version.AddQuestion(NumberQuestion(), 2m, Now);
        version.AddQuestion(NumberQuestion(), null, Now);

        version.ValidateForPublish(exam, hasAssignments: true, Now).Should().BeEmpty();
        version.Publish(Admin, Now);
        exam.MarkPublished(Admin, Now);

        version.QuestionCount.Should().Be(2);
        version.MaxScore.Should().Be(3m);
        exam.Status.Should().Be(ExamStatus.Published);
        var modify = () => version.AddQuestion(NumberQuestion(), null, Now);
        modify.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.VersionImmutable);
        var settings = () => version.UpdateSettings(version.Settings with { DurationMinutes = 10 });
        settings.Should().Throw<DomainException>();
    }

    [Fact]
    public void Snapshot_is_independent_from_the_bank_question()
    {
        var source = NumberQuestion();
        var version = NewExam().Versions.Single();
        var snapshot = version.AddQuestion(source, null, Now)!;

        source.Update(source.ToData() with { Content = "Nội dung đã sửa", CorrectAnswerNumber = 99 }, Admin, Now);

        snapshot.Content.Should().Be("2 + 2 = ?");
        snapshot.CorrectAnswerNumber.Should().Be(4);
        snapshot.SourceQuestionId.Should().Be(source.Id);
    }

    [Fact]
    public void Adding_the_same_bank_question_twice_is_ignored()
    {
        var source = NumberQuestion();
        var version = NewExam().Versions.Single();

        version.AddQuestion(source, null, Now).Should().NotBeNull();
        version.AddQuestion(source, null, Now).Should().BeNull();
        version.Questions.Should().ContainSingle();
    }

    [Fact]
    public void Reorder_requires_the_complete_list()
    {
        var version = NewExam().Versions.Single();
        var a = version.AddQuestion(NumberQuestion(), null, Now)!.WithId();
        var b = version.AddQuestion(NumberQuestion(), null, Now)!.WithId();

        var partial = () => version.Reorder([a.Id]);
        partial.Should().Throw<DomainException>();

        version.Reorder([b.Id, a.Id]);
        b.QuestionOrder.Should().Be(1);
        a.QuestionOrder.Should().Be(2);
    }

    [Fact]
    public void Copy_as_draft_duplicates_settings_and_questions()
    {
        var exam = NewExam();
        var v1 = exam.Versions.Single();
        v1.AddQuestion(NumberQuestion(), 3m, Now);
        v1.Publish(Admin, Now);

        var v2 = v1.CopyAsDraft(exam.Id, 2, Admin, Now);

        v2.IsDraft.Should().BeTrue();
        v2.Settings.Should().Be(v1.Settings);
        v2.Questions.Should().ContainSingle().Which.Score.Should().Be(3m);
        v2.Questions.Single().Should().NotBeSameAs(v1.Questions.Single());
    }

    [Fact]
    public void Exam_status_transitions_are_enforced()
    {
        var exam = NewExam();

        var closeDraft = () => exam.Close(Admin, Now);
        closeDraft.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.InvalidStateTransition);

        exam.MarkPublished(Admin, Now);
        exam.Close(Admin, Now);
        exam.Status.Should().Be(ExamStatus.Closed);
        exam.Reopen(Admin, Now);
        exam.Status.Should().Be(ExamStatus.Published);
        var changeCode = () => exam.ChangeCode("NEW");
        changeCode.Should().Throw<DomainException>();
    }

    [Fact]
    public void Assignments_are_replaced_as_a_set()
    {
        var exam = NewExam();
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var u1 = Guid.NewGuid();

        exam.SetAssignments([g1, g2], [u1], Admin, Now).Should().BeTrue();
        exam.SetAssignments([g2], [u1], Admin, Now).Should().BeTrue();
        exam.SetAssignments([g2], [u1], Admin, Now).Should().BeFalse();

        exam.Assignments.Select(a => a.GroupId ?? a.UserId).Should().BeEquivalentTo(new Guid?[] { g2, u1 });
    }

    private static Exam NewExam(
        int maxAttempts = 1, AccessMode accessMode = AccessMode.Public, DateTime? endAt = null) =>
        Exam.Create(
            "EX-1",
            new ExamDetails("Đề thử", null, null, null, endAt, maxAttempts, accessMode, RetakeScoringPolicy.Highest),
            new VersionSettings(60, 50m, ScoreVisibility.Immediate, ReviewPolicy.Never),
            Admin,
            Now);

    private static ExamVersion CreateVersion(Exam exam, VersionSettings settings)
    {
        var version = exam.Versions.Single();
        version.UpdateSettings(settings);
        return version;
    }

    private static Question NumberQuestion() =>
        Question.Create(
            $"Q-{Guid.NewGuid():N}",
            new QuestionData(null, "2 + 2 = ?", ContentFormat.Plain, QuestionType.FillIn, AnswerDataType.Number, 4, 0, false, false,
                null, 1m, [], []),
            Admin,
            Now).WithId();
}
