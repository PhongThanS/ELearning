using ELearning.Domain.Attempts;
using ELearning.Domain.Audit;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal sealed class ExamAttemptConfiguration : IEntityTypeConfiguration<ExamAttempt>
{
    public void Configure(EntityTypeBuilder<ExamAttempt> builder)
    {
        builder.ConfigureEntity("ExamAttempts");
        builder.Property(a => a.CancelReason).HasMaxLength(500);
        builder.Property(a => a.StartedIp).IsIp();
        builder.Property(a => a.SubmittedIp).IsIp();
        builder.Property(a => a.StartedUserAgent).HasMaxLength(500);

        builder.HasOne<Exam>().WithMany().HasForeignKey(a => a.ExamId).HasConstraintName("FK_ExamAttempts_Exam");
        builder.HasOne<ExamVersion>().WithMany().HasForeignKey(a => a.ExamVersionId)
            .HasConstraintName("FK_ExamAttempts_Version");
        builder.HasUserReference(a => a.UserId, "FK_ExamAttempts_User");
        builder.HasUserReference(a => a.CancelledBy, "FK_ExamAttempts_CancelledBy");

        builder.HasIndex(a => new { a.UserId, a.ExamId, a.AttemptNumber }).IsUnique()
            .HasDatabaseName("UQ_ExamAttempts_User_Exam_Number");

        // D-07: mỗi (UserId, ExamId) tối đa 1 lượt đang làm
        builder.HasIndex(a => new { a.UserId, a.ExamId }).IsUnique().HasFilter("[Status] = 'IN_PROGRESS'")
            .HasDatabaseName("UX_ExamAttempts_OneInProgress");

        // D-05: job tự nộp
        builder.HasIndex(a => a.ExpiredAt).HasFilter("[Status] = 'IN_PROGRESS'")
            .HasDatabaseName("IX_ExamAttempts_InProgress_Expired");
        builder.HasIndex(a => new { a.ExamId, a.Status })
            .IncludeProperties(a => new { a.UserId, a.SubmittedAt })
            .HasDatabaseName("IX_ExamAttempts_Exam");
        builder.HasIndex(a => new { a.ExamVersionId, a.Status }).HasDatabaseName("IX_ExamAttempts_Version");

        builder.HasMany(a => a.Questions).WithOne().HasForeignKey(q => q.AttemptId)
            .HasConstraintName("FK_AttemptQuestions_Attempt");

        builder.ToTable(t =>
        {
            t.HasEnumCheck<AttemptStatus>("CK_ExamAttempts_Status", "Status");
            t.HasEnumCheck<SubmitReason>("CK_ExamAttempts_SubmitReason", "SubmitReason");
            t.HasCheckConstraint("CK_ExamAttempts_Time", "[ExpiredAt] > [StartedAt]");
            t.HasCheckConstraint(
                "CK_ExamAttempts_Submitted",
                "([Status] IN ('SUBMITTED','AUTO_SUBMITTED') AND [SubmittedAt] IS NOT NULL AND [SubmitReason] IS NOT NULL) OR ([Status] IN ('IN_PROGRESS','CANCELLED'))");
        });
    }
}

internal sealed class AttemptQuestionConfiguration : IEntityTypeConfiguration<AttemptQuestion>
{
    public void Configure(EntityTypeBuilder<AttemptQuestion> builder)
    {
        builder.ConfigureEntity("AttemptQuestions");
        builder.Property(q => q.OptionOrder).HasMaxLength(200).IsUnicode(false);

        builder.HasOne<ExamQuestion>().WithMany().HasForeignKey(q => q.ExamQuestionId)
            .HasConstraintName("FK_AttemptQuestions_ExamQuestion");
        builder.HasIndex(q => new { q.AttemptId, q.QuestionOrder }).IsUnique()
            .HasDatabaseName("UQ_AttemptQuestions_Order");
        builder.HasIndex(q => new { q.AttemptId, q.ExamQuestionId }).IsUnique()
            .HasDatabaseName("UQ_AttemptQuestions_Question");

        builder.HasOne(q => q.Answer).WithOne().HasForeignKey<AttemptAnswer>(a => a.AttemptQuestionId)
            .HasConstraintName("FK_AttemptAnswers_Question");
    }
}

internal sealed class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.ConfigureEntity("AttemptAnswers");
        builder.Property(a => a.AnswerText).HasMaxLength(1000);
        builder.Property(a => a.AnswerNumber).HasColumnType(ConfigurationExtensions.SqlDecimalAnswer);
        builder.HasIndex(a => a.AttemptQuestionId).IsUnique().HasDatabaseName("UQ_AttemptAnswers_Question");

        builder.HasMany(a => a.SelectedOptions).WithOne().HasForeignKey(o => o.AttemptAnswerId)
            .HasConstraintName("FK_AttemptAnswerOptions_Answer");
    }
}

internal sealed class AttemptAnswerOptionConfiguration : IEntityTypeConfiguration<AttemptAnswerOption>
{
    public void Configure(EntityTypeBuilder<AttemptAnswerOption> builder)
    {
        builder.ToTable("AttemptAnswerOptions");
        builder.HasKey(o => new { o.AttemptAnswerId, o.OptionCode }).HasName("PK_AttemptAnswerOptions");
        builder.Property(o => o.OptionCode).IsCode(10);
    }
}

internal sealed class AttemptEventConfiguration : IEntityTypeConfiguration<AttemptEvent>
{
    public void Configure(EntityTypeBuilder<AttemptEvent> builder)
    {
        builder.ToTable("AttemptEvents", t => t.HasEnumCheck<AttemptEventType>("CK_AttemptEvents_Type", "EventType"));
        builder.HasKey(e => e.Id).HasName("PK_AttemptEvents");
        builder.Property(e => e.Id).UseIdentityColumn();
        builder.Property(e => e.IpAddress).IsIp();
        builder.Property(e => e.Detail).HasMaxLength(500);
        builder.HasOne<ExamAttempt>().WithMany().HasForeignKey(e => e.AttemptId).HasConstraintName("FK_AttemptEvents_Attempt");
        builder.HasIndex(e => new { e.AttemptId, e.ServerTime }).HasDatabaseName("IX_AttemptEvents_Attempt");
    }
}

internal sealed class ExamResultConfiguration : IEntityTypeConfiguration<ExamResult>
{
    public void Configure(EntityTypeBuilder<ExamResult> builder)
    {
        builder.ConfigureEntity("ExamResults");
        builder.Property(r => r.Percentage).HasColumnType(ConfigurationExtensions.SqlPercentage);

        builder.HasOne<ExamAttempt>().WithOne().HasForeignKey<ExamResult>(r => r.AttemptId)
            .HasConstraintName("FK_ExamResults_Attempt");
        builder.HasOne<Exam>().WithMany().HasForeignKey(r => r.ExamId).HasConstraintName("FK_ExamResults_Exam");
        builder.HasOne<ExamVersion>().WithMany().HasForeignKey(r => r.ExamVersionId)
            .HasConstraintName("FK_ExamResults_Version");
        builder.HasUserReference(r => r.UserId, "FK_ExamResults_User");

        builder.HasIndex(r => r.AttemptId).IsUnique().HasDatabaseName("UQ_ExamResults_Attempt");
        builder.HasIndex(r => r.ExamId)
            .IncludeProperties(r => new { r.UserId, r.TotalScore, r.Percentage, r.Passed, r.SubmittedAt })
            .HasDatabaseName("IX_ExamResults_Exam");
        builder.HasIndex(r => new { r.UserId, r.SubmittedAt }).IsDescending(false, true)
            .HasDatabaseName("IX_ExamResults_User");

        builder.HasMany(r => r.History).WithOne().HasForeignKey(h => h.ExamResultId)
            .HasConstraintName("FK_ExamResultHistory_Result");

        builder.ToTable(t => t.HasCheckConstraint("CK_ExamResults_MaxScore", "[MaxScore] > 0"));
    }
}

internal sealed class ExamResultHistoryConfiguration : IEntityTypeConfiguration<ExamResultHistory>
{
    public void Configure(EntityTypeBuilder<ExamResultHistory> builder)
    {
        builder.ToTable("ExamResultHistory");
        builder.HasKey(h => h.Id).HasName("PK_ExamResultHistory");
        builder.Property(h => h.Id).UseIdentityColumn();
        builder.Property(h => h.Percentage).HasColumnType(ConfigurationExtensions.SqlPercentage);
        builder.HasOne<AnswerKeyCorrection>().WithMany().HasForeignKey(h => h.AnswerKeyCorrectionId)
            .HasConstraintName("FK_ExamResultHistory_Correction");
        builder.HasIndex(h => h.ExamResultId).HasDatabaseName("IX_ExamResultHistory_Result");
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id).HasName("PK_AuditLogs");
        builder.Property(a => a.Id).UseIdentityColumn();
        builder.Property(a => a.Action).HasMaxLength(100).IsUnicode(false).IsRequired();
        builder.Property(a => a.EntityName).HasMaxLength(100).IsUnicode(false);
        builder.Property(a => a.Reason).HasMaxLength(1000);
        builder.Property(a => a.IpAddress).IsIp();
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.Property(a => a.TraceId).HasMaxLength(64).IsUnicode(false);

        builder.HasIndex(a => a.CreatedAt).IsDescending().HasDatabaseName("IX_AuditLogs_CreatedAt");
        builder.HasIndex(a => new { a.UserId, a.CreatedAt }).IsDescending(false, true).HasDatabaseName("IX_AuditLogs_User");
        builder.HasIndex(a => new { a.EntityName, a.EntityId }).HasDatabaseName("IX_AuditLogs_Entity");
    }
}
