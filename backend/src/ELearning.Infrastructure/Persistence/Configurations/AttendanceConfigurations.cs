using ELearning.Domain.Attendance;
using ELearning.Domain.Classes;
using ELearning.Domain.Enums;
using ELearning.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

/// <summary>Buổi học + điểm danh (D-31, docs/04b-csdl-luot-thi-ket-qua.md mục 3a).</summary>
internal sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.ConfigureEntity("AttendanceSessions");
        builder.Property(s => s.Topic).HasMaxLength(AttendanceSession.MaxTopicLength);
        builder.Property(s => s.NextHomework).HasMaxLength(AttendanceSession.MaxHomeworkLength);

        builder.HasOne<Classroom>().WithMany().HasForeignKey(s => s.ClassroomId).HasConstraintName("FK_AttendanceSessions_Classroom");
        builder.HasUserReference(s => s.CreatedBy, "FK_AttendanceSessions_CreatedBy");
        builder.HasUserReference(s => s.UpdatedBy, "FK_AttendanceSessions_UpdatedBy");

        builder.HasMany(s => s.Records).WithOne().HasForeignKey(r => r.SessionId)
            .HasConstraintName("FK_AttendanceRecords_Session");

        // Một buổi mỗi lớp mỗi ngày; cũng là index tra buổi theo tháng
        builder.HasIndex(s => new { s.ClassroomId, s.SessionDate }).IsUnique().HasDatabaseName("UQ_AttendanceSessions_Classroom_Date");
    }
}

internal sealed class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ConfigureEntity("AttendanceRecords");
        builder.Property(r => r.HomeworkScore).HasPrecision(4, 1);
        builder.Property(r => r.Note).HasMaxLength(AttendanceRecord.MaxNoteLength);

        builder.HasUserReference(r => r.StudentId, "FK_AttendanceRecords_Student");
        builder.HasUserReference(r => r.UpdatedBy, "FK_AttendanceRecords_UpdatedBy");

        builder.HasIndex(r => new { r.SessionId, r.StudentId }).IsUnique().HasDatabaseName("UQ_AttendanceRecords_Session_Student");
        // Lịch sử theo học sinh (thống kê tháng)
        builder.HasIndex(r => r.StudentId).HasDatabaseName("IX_AttendanceRecords_Student");

        builder.ToTable(t =>
        {
            t.HasEnumCheck<AttendanceStatus>("CK_AttendanceRecords_Status", "Status");
            t.HasEnumCheck<HomeworkStatus>("CK_AttendanceRecords_HomeworkStatus", "HomeworkStatus");
            t.HasCheckConstraint("CK_AttendanceRecords_Score", "\"HomeworkScore\" IS NULL OR (\"HomeworkScore\" >= 0 AND \"HomeworkScore\" <= 10)");
        });
    }
}

internal sealed class StudentContactConfiguration : IEntityTypeConfiguration<StudentContact>
{
    public void Configure(EntityTypeBuilder<StudentContact> builder)
    {
        builder.ToTable("StudentContacts");
        builder.HasKey(c => c.UserId).HasName("PK_StudentContacts");
        builder.Property(c => c.ParentName).HasMaxLength(StudentContact.MaxParentNameLength);
        builder.Property(c => c.ParentPhone).HasMaxLength(StudentContact.MaxPhoneLength).IsUnicode(false);
        builder.HasOne<User>().WithOne().HasForeignKey<StudentContact>(c => c.UserId).HasConstraintName("FK_StudentContacts_User");
        builder.HasUserReference(c => c.UpdatedBy, "FK_StudentContacts_UpdatedBy");
    }
}
