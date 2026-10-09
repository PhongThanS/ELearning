using ELearning.Domain.Classes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal sealed class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.ConfigureEntity("Classrooms");
        builder.Property(c => c.Code).IsCode(Classroom.MaxCodeLength);
        builder.Property(c => c.Name).HasMaxLength(Classroom.MaxNameLength).IsRequired();
        builder.Property(c => c.SchoolYear).HasMaxLength(Classroom.MaxSchoolYearLength);
        builder.Property(c => c.Description).HasMaxLength(Classroom.MaxDescriptionLength);
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("UQ_Classrooms_Code");
        builder.HasUserReference(c => c.CreatedBy, "FK_Classrooms_CreatedBy");
        builder.HasUserReference(c => c.UpdatedBy, "FK_Classrooms_UpdatedBy");

        builder.HasMany(c => c.Students).WithOne().HasForeignKey(s => s.ClassroomId)
            .HasConstraintName("FK_ClassroomStudents_Classroom");

        builder.ToTable(t => t.HasCheckConstraint("CK_Classrooms_Dates", "\"EndDate\" IS NULL OR \"StartDate\" IS NULL OR \"EndDate\" >= \"StartDate\""));
    }
}

/// <summary>Bảng nối nhiều-nhiều học viên ↔ lớp (D-28).</summary>
internal sealed class ClassroomStudentConfiguration : IEntityTypeConfiguration<ClassroomStudent>
{
    public void Configure(EntityTypeBuilder<ClassroomStudent> builder)
    {
        builder.ToTable("ClassroomStudents");
        builder.HasKey(s => new { s.ClassroomId, s.UserId }).HasName("PK_ClassroomStudents");
        builder.HasUserReference(s => s.UserId, "FK_ClassroomStudents_User");
        // Tra "lớp của học viên" (trang Lớp của tôi, quyền xem đề) theo UserId
        builder.HasIndex(s => s.UserId).HasDatabaseName("IX_ClassroomStudents_User");
    }
}
