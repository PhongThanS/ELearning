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
        builder.Property(c => c.StudentPhone).HasMaxLength(StudentContact.MaxPhoneLength).IsUnicode(false);
        builder.Property(c => c.ParentPhone).HasMaxLength(StudentContact.MaxPhoneLength).IsUnicode(false);
        builder.HasOne<User>().WithOne().HasForeignKey<StudentContact>(c => c.UserId).HasConstraintName("FK_StudentContacts_User");
        builder.HasUserReference(c => c.UpdatedBy, "FK_StudentContacts_UpdatedBy");
    }
}

/// <summary>Gửi tin Zalo cho phụ huynh (D-45, docs/04b-csdl-luot-thi-ket-qua.md mục 3b).</summary>
internal sealed class ZaloBroadcastConfiguration : IEntityTypeConfiguration<ELearning.Domain.Zalo.ZaloBroadcast>
{
    public void Configure(EntityTypeBuilder<ELearning.Domain.Zalo.ZaloBroadcast> builder)
    {
        builder.ConfigureEntity("ZaloBroadcasts");
        builder.Property(b => b.Content).HasMaxLength(ELearning.Domain.Zalo.ZaloBroadcast.MaxTextLength);

        builder.HasOne<Classroom>().WithMany().HasForeignKey(b => b.ClassroomId).HasConstraintName("FK_ZaloBroadcasts_Classroom");
        builder.HasUserReference(b => b.CreatedBy, "FK_ZaloBroadcasts_CreatedBy");
        builder.HasMany(b => b.Messages).WithOne().HasForeignKey(m => m.BroadcastId).HasConstraintName("FK_ZaloMessages_Broadcast");

        // Danh sách đợt gửi của lớp (mới nhất trước) và kiểm tra "buổi này đã gửi chưa"
        builder.HasIndex(b => new { b.ClassroomId, b.CreatedAt }).HasDatabaseName("IX_ZaloBroadcasts_Classroom_CreatedAt");
        builder.ToTable(t => t.HasEnumCheck<ZaloMessageKind>("CK_ZaloBroadcasts_Kind", "Kind"));
    }
}

internal sealed class ZaloMessageConfiguration : IEntityTypeConfiguration<ELearning.Domain.Zalo.ZaloMessage>
{
    public void Configure(EntityTypeBuilder<ELearning.Domain.Zalo.ZaloMessage> builder)
    {
        builder.ConfigureEntity("ZaloMessages");
        builder.Property(m => m.RecipientName).HasMaxLength(StudentContact.MaxParentNameLength);
        builder.Property(m => m.Phone).HasMaxLength(ELearning.Domain.Zalo.ZaloMessage.MaxPhoneLength).IsUnicode(false);
        builder.Property(m => m.TemplateId).HasMaxLength(100).IsUnicode(false);
        builder.Property(m => m.TemplateData).HasColumnType("text");
        builder.Property(m => m.TrackingId).HasMaxLength(40).IsUnicode(false);
        builder.Property(m => m.ProviderMessageId).HasMaxLength(100).IsUnicode(false);
        builder.Property(m => m.ErrorCode).HasMaxLength(50).IsUnicode(false);
        builder.Property(m => m.ErrorMessage).HasMaxLength(ELearning.Domain.Zalo.ZaloMessage.MaxErrorLength);

        // Status là concurrency token: hủy / gửi lại chỉ thắng khi tin chưa bị job nhận (UPDATE ... WHERE Status = giá trị cũ)
        builder.Property(m => m.Status).IsConcurrencyToken();

        builder.HasUserReference(m => m.StudentId, "FK_ZaloMessages_Student");

        builder.HasIndex(m => m.TrackingId).IsUnique().HasDatabaseName("UQ_ZaloMessages_TrackingId");
        builder.HasIndex(m => m.BroadcastId).HasDatabaseName("IX_ZaloMessages_Broadcast");
        // Job quét hàng đợi: chỉ các tin còn chờ / đang gửi
        builder.HasIndex(m => new { m.Status, m.CreatedAt }).HasDatabaseName("IX_ZaloMessages_Queue")
            .HasFilter("\"Status\" IN ('PENDING','SENDING')");
        builder.ToTable(t => t.HasEnumCheck<ZaloMessageStatus>("CK_ZaloMessages_Status", "Status"));
    }
}
