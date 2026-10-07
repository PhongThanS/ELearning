using ELearning.Domain.Classes;
using ELearning.Domain.Common;

namespace ELearning.UnitTests.Classes;

public class ClassroomDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 2, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Admin = Guid.NewGuid();

    private static Classroom NewClass(DateOnly? start = null, DateOnly? end = null) =>
        new(" 10A1 ", new ClassroomDetails(" Toán 10A1 ", " 2026-2027 ", start, end, "  "), Admin, Now);

    [Fact]
    public void Create_trims_fields_and_starts_active()
    {
        var classroom = NewClass();

        classroom.Code.Should().Be("10A1");
        classroom.Name.Should().Be("Toán 10A1");
        classroom.SchoolYear.Should().Be("2026-2027");
        classroom.Description.Should().BeNull();
        classroom.IsActive.Should().BeTrue();
    }

    [Fact]
    public void End_date_before_start_date_is_rejected()
    {
        var create = () => NewClass(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 1));

        create.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_CLASSROOM");
    }

    [Fact]
    public void Adding_students_ignores_duplicates_and_existing_members()
    {
        var classroom = NewClass();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        classroom.AddStudents([a, a, b], Now).Should().Be(2);
        classroom.AddStudents([b], Now).Should().Be(0);
        classroom.Students.Should().HaveCount(2);
        classroom.RemoveStudent(a).Should().BeTrue();
        classroom.RemoveStudent(a).Should().BeFalse();
        classroom.Students.Should().ContainSingle(s => s.UserId == b);
    }

    [Fact]
    public void Deactivate_and_reactivate_records_who_and_when()
    {
        var classroom = NewClass();
        var later = Now.AddHours(1);

        classroom.SetActive(false, Admin, later);
        classroom.IsActive.Should().BeFalse();
        classroom.UpdatedAt.Should().Be(later);
        classroom.SetActive(true, Admin, later);
        classroom.IsActive.Should().BeTrue();
    }
}
