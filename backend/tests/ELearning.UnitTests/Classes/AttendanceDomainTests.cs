using ELearning.Domain.Attendance;
using ELearning.Domain.Common;
using ELearning.Domain.Enums;

namespace ELearning.UnitTests.Classes;

public class AttendanceDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 8, 2, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Teacher = Guid.NewGuid();

    private static AttendanceSession NewSession() => new(Guid.NewGuid(), new DateOnly(2026, 11, 8), Teacher, Now);

    [Theory]
    [InlineData("0", true)]
    [InlineData("7.5", true)]
    [InlineData("10", true)]
    [InlineData("7.3", false)]
    [InlineData("-0.5", false)]
    [InlineData("10.5", false)]
    public void Homework_score_is_0_to_10_in_half_steps(string score, bool valid) =>
        AttendanceRecord.IsValidScore(decimal.Parse(score, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(valid);

    [Fact]
    public void Mark_twice_updates_the_same_record()
    {
        var session = NewSession();
        var student = Guid.NewGuid();

        session.Mark(student, new AttendanceEntry(AttendanceStatus.Late, HomeworkStatus.Partial, 5m, "  "), Teacher, Now);
        session.Mark(student, new AttendanceEntry(AttendanceStatus.Present, HomeworkStatus.Done, 8m, " Tốt "), Teacher, Now);

        var record = session.Records.Should().ContainSingle().Subject;
        record.Status.Should().Be(AttendanceStatus.Present);
        record.HomeworkScore.Should().Be(8m);
        record.Note.Should().Be("Tốt");
    }

    [Fact]
    public void Invalid_score_is_rejected()
    {
        var mark = () => NewSession().Mark(Guid.NewGuid(), new AttendanceEntry(null, null, 11m, null), Teacher, Now);

        mark.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrorCodes.InvalidAttendance);
    }

    [Fact]
    public void Reset_clears_values_but_keeps_rows()
    {
        var session = NewSession();
        session.UpdateNotes("Chủ đề", "BTVN", Teacher, Now);
        session.Mark(Guid.NewGuid(), new AttendanceEntry(AttendanceStatus.Present, HomeworkStatus.Excellent, 10m, "Giỏi"), Teacher, Now);

        session.Reset(Teacher, Now);

        session.Topic.Should().BeNull();
        session.Records.Should().ContainSingle()
            .Which.Should().Match<AttendanceRecord>(r => r.Status == null && r.HomeworkStatus == null && r.HomeworkScore == null && r.Note == null);
    }

    [Theory]
    [InlineData("0912 345 678", true)]
    [InlineData("+84.912.345.678", true)]
    [InlineData("12345", false)]
    [InlineData("09123abc45", false)]
    public void Parent_phone_format(string phone, bool valid) => StudentContact.IsValidPhone(phone).Should().Be(valid);
}
