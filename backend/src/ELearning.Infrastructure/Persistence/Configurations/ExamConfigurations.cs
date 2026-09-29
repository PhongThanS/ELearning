using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using ELearning.Domain.Identity;
using ELearning.Domain.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ConfigureEntity("Exams");
        builder.Property(e => e.Code).IsCode(100);
        builder.Property(e => e.Name).HasMaxLength(300).IsRequired();

        builder.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UQ_Exams_Code");
        builder.HasIndex(e => e.Status)
            .IncludeProperties(e => new { e.StartAt, e.EndAt, e.AccessMode })
            .HasDatabaseName("IX_Exams_Status");

        builder.HasUserReference(e => e.CreatedBy, "FK_Exams_CreatedBy");
        builder.HasUserReference(e => e.UpdatedBy, "FK_Exams_UpdatedBy");

        builder.HasMany(e => e.Versions).WithOne().HasForeignKey(v => v.ExamId).HasConstraintName("FK_ExamVersions_Exam").OnDelete(DeleteBehavior.ClientCascade);
        builder.HasMany(e => e.Assignments).WithOne().HasForeignKey(a => a.ExamId)
            .HasConstraintName("FK_ExamAssignments_Exam").OnDelete(DeleteBehavior.ClientCascade);

        builder.ToTable(t =>
        {
            t.HasEnumCheck<ExamStatus>("CK_Exams_Status", "Status");
            t.HasEnumCheck<AccessMode>("CK_Exams_AccessMode", "AccessMode");
            t.HasEnumCheck<RetakeScoringPolicy>("CK_Exams_RetakeScoring", "RetakeScoringPolicy");
            t.HasCheckConstraint(
                "CK_Exams_MaxAttempts", $"[MaxAttempts] BETWEEN {Exam.MinAttempts} AND {Exam.MaxAttemptsLimit}");
            t.HasCheckConstraint("CK_Exams_Window", "[StartAt] IS NULL OR [EndAt] IS NULL OR [StartAt] < [EndAt]");
        });
    }
}

internal sealed class ExamVersionConfiguration : IEntityTypeConfiguration<ExamVersion>
{
    public void Configure(EntityTypeBuilder<ExamVersion> builder)
    {
        builder.ConfigureEntity("ExamVersions");
        builder.Property(v => v.PassPercentage).HasColumnType(ConfigurationExtensions.SqlPercentage);
        builder.Ignore(v => v.FixedQuestions);

        builder.HasIndex(v => new { v.ExamId, v.VersionNumber }).IsUnique().HasDatabaseName("UQ_ExamVersions_Number");

        // D-04: tối đa 1 version PUBLISHED và 1 version DRAFT cho mỗi đề.
        // Hai index cùng cột ExamId nên phải dùng overload có tên, nếu không EF gộp làm một.
        builder.HasIndex(v => v.ExamId, "UX_ExamVersions_OnePublished").IsUnique().HasFilter("[Status] = 'PUBLISHED'");
        builder.HasIndex(v => v.ExamId, "UX_ExamVersions_OneDraft").IsUnique().HasFilter("[Status] = 'DRAFT'");

        builder.HasUserReference(v => v.PublishedBy, "FK_ExamVersions_PublishedBy");
        builder.HasUserReference(v => v.CreatedBy, "FK_ExamVersions_CreatedBy");

        builder.HasMany(v => v.Questions).WithOne().HasForeignKey(q => q.ExamVersionId)
            .HasConstraintName("FK_ExamQuestions_Version").OnDelete(DeleteBehavior.ClientCascade);
        builder.HasMany(v => v.PoolRules).WithOne().HasForeignKey(r => r.ExamVersionId)
            .HasConstraintName("FK_ExamPoolRules_Version").OnDelete(DeleteBehavior.ClientCascade);

        builder.ToTable(t =>
        {
            t.HasEnumCheck<ExamVersionStatus>("CK_ExamVersions_Status", "Status");
            t.HasEnumCheck<ScoreVisibility>("CK_ExamVersions_ScoreVisibility", "ScoreVisibility");
            t.HasEnumCheck<ReviewPolicy>("CK_ExamVersions_ReviewPolicy", "ReviewPolicy");
            t.HasCheckConstraint("CK_ExamVersions_Duration", "[DurationMinutes] BETWEEN 1 AND 600");
            t.HasCheckConstraint("CK_ExamVersions_Pass", "[PassPercentage] BETWEEN 0 AND 100");
        });
    }
}

internal sealed class ExamQuestionConfiguration : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.ConfigureEntity("ExamQuestions");
        builder.Property(q => q.Content).IsRequired();
        builder.Property(q => q.SourceRowVersion).HasColumnType("binary(8)");
        builder.Property(q => q.CorrectAnswerNumber).HasColumnType(ConfigurationExtensions.SqlDecimalAnswer);
        builder.Property(q => q.NumericTolerance).HasColumnType(ConfigurationExtensions.SqlDecimalAnswer);

        builder.HasIndex(q => new { q.ExamVersionId, q.QuestionOrder }).IsUnique()
            .HasDatabaseName("UQ_ExamQuestions_Order");
        builder.HasIndex(q => new { q.ExamVersionId, q.SourceQuestionId }).IsUnique()
            .HasFilter("[SourceQuestionId] IS NOT NULL")
            .HasDatabaseName("UX_ExamQuestions_Source");

        builder.HasOne<Question>().WithMany().HasForeignKey(q => q.SourceQuestionId)
            .HasConstraintName("FK_ExamQuestions_SourceQuestion");
        builder.HasOne<ExamPoolRule>().WithMany().HasForeignKey(q => q.PoolRuleId)
            .HasConstraintName("FK_ExamQuestions_PoolRule");
        builder.HasIndex(q => q.PoolRuleId).HasFilter("[PoolRuleId] IS NOT NULL").HasDatabaseName("IX_ExamQuestions_PoolRule");
        builder.HasUserReference(q => q.VoidedBy, "FK_ExamQuestions_VoidedBy");

        builder.HasMany(q => q.Options).WithOne().HasForeignKey(o => o.ExamQuestionId)
            .HasConstraintName("FK_ExamQuestionOptions_Question").OnDelete(DeleteBehavior.ClientCascade);
        builder.HasMany(q => q.AcceptedAnswers).WithOne().HasForeignKey(a => a.ExamQuestionId)
            .HasConstraintName("FK_ExamQuestionAcceptedAnswers_Question").OnDelete(DeleteBehavior.ClientCascade);

        builder.ToTable(t =>
        {
            t.HasEnumCheck<ContentFormat>("CK_ExamQuestions_ContentFormat", "ContentFormat");
            t.HasEnumCheck<QuestionType>("CK_ExamQuestions_Type", "QuestionType");
            t.HasEnumCheck<AnswerDataType>("CK_ExamQuestions_AnswerDataType", "AnswerDataType");
            t.HasCheckConstraint("CK_ExamQuestions_Score", "[Score] > 0");
        });
    }
}

internal sealed class ExamPoolRuleConfiguration : IEntityTypeConfiguration<ExamPoolRule>
{
    public void Configure(EntityTypeBuilder<ExamPoolRule> builder)
    {
        builder.ConfigureEntity("ExamPoolRules");
        // Id do domain sinh sẵn (câu ứng viên cần tham chiếu ngay): không để EF coi entity mới là entity đã tồn tại
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Tag).HasMaxLength(Question.MaxTagLength);
        builder.HasIndex(r => new { r.ExamVersionId, r.RuleOrder }).IsUnique().HasDatabaseName("UQ_ExamPoolRules_Order");
        builder.HasOne<QuestionCategory>().WithMany().HasForeignKey(r => r.CategoryId)
            .HasConstraintName("FK_ExamPoolRules_Category");

        builder.ToTable(t =>
        {
            t.HasEnumCheck<QuestionDifficulty>("CK_ExamPoolRules_Difficulty", "Difficulty");
            t.HasEnumCheck<QuestionType>("CK_ExamPoolRules_Type", "QuestionType");
            t.HasCheckConstraint("CK_ExamPoolRules_DrawCount", "[DrawCount] BETWEEN 1 AND 500");
            t.HasCheckConstraint("CK_ExamPoolRules_Score", "[ScorePerQuestion] > 0");
        });
    }
}

internal sealed class ExamQuestionOptionConfiguration : IEntityTypeConfiguration<ExamQuestionOption>
{
    public void Configure(EntityTypeBuilder<ExamQuestionOption> builder)
    {
        builder.ConfigureEntity("ExamQuestionOptions");
        builder.Property(o => o.OptionCode).IsCode(10);
        builder.Property(o => o.Content).HasMaxLength(2000).IsRequired();
        builder.HasIndex(o => new { o.ExamQuestionId, o.OptionCode }).IsUnique()
            .HasDatabaseName("UQ_ExamQuestionOptions_Code");
    }
}

internal sealed class ExamQuestionAcceptedAnswerConfiguration : IEntityTypeConfiguration<ExamQuestionAcceptedAnswer>
{
    public void Configure(EntityTypeBuilder<ExamQuestionAcceptedAnswer> builder)
    {
        builder.ConfigureEntity("ExamQuestionAcceptedAnswers");
        builder.Property(a => a.AnswerText).HasMaxLength(1000).IsRequired();
        builder.HasIndex(a => new { a.ExamQuestionId, a.DisplayOrder }).IsUnique()
            .HasDatabaseName("UQ_ExamQuestionAcceptedAnswers_Order");
    }
}

internal sealed class ExamAssignmentConfiguration : IEntityTypeConfiguration<ExamAssignment>
{
    public void Configure(EntityTypeBuilder<ExamAssignment> builder)
    {
        builder.ConfigureEntity("ExamAssignments");
        builder.HasOne<UserGroup>().WithMany().HasForeignKey(a => a.GroupId).HasConstraintName("FK_ExamAssignments_Group");
        builder.HasUserReference(a => a.UserId, "FK_ExamAssignments_User");
        builder.HasUserReference(a => a.CreatedBy, "FK_ExamAssignments_CreatedBy");

        builder.HasIndex(a => new { a.ExamId, a.GroupId }).IsUnique().HasFilter("[GroupId] IS NOT NULL")
            .HasDatabaseName("UX_ExamAssignments_Group");
        builder.HasIndex(a => new { a.ExamId, a.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL")
            .HasDatabaseName("UX_ExamAssignments_User");
        builder.HasIndex(a => a.GroupId).HasFilter("[GroupId] IS NOT NULL").HasDatabaseName("IX_ExamAssignments_GroupId");
        builder.HasIndex(a => a.UserId).HasFilter("[UserId] IS NOT NULL").HasDatabaseName("IX_ExamAssignments_UserId");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ExamAssignments_Target",
            "([GroupId] IS NULL AND [UserId] IS NOT NULL) OR ([GroupId] IS NOT NULL AND [UserId] IS NULL)"));
    }
}

internal sealed class ExamUserOverrideConfiguration : IEntityTypeConfiguration<ExamUserOverride>
{
    public void Configure(EntityTypeBuilder<ExamUserOverride> builder)
    {
        builder.ToTable("ExamUserOverrides", t => t.HasCheckConstraint(
            "CK_ExamUserOverrides_Extra", $"[ExtraAttempts] BETWEEN 0 AND {Exam.MaxAttemptsLimit}"));
        builder.HasKey(o => new { o.ExamId, o.UserId }).HasName("PK_ExamUserOverrides");
        builder.Property(o => o.Note).HasMaxLength(500);
        builder.HasOne<Exam>().WithMany().HasForeignKey(o => o.ExamId).HasConstraintName("FK_ExamUserOverrides_Exam");
        builder.HasUserReference(o => o.UserId, "FK_ExamUserOverrides_User");
        builder.HasUserReference(o => o.UpdatedBy, "FK_ExamUserOverrides_UpdatedBy");
    }
}

internal sealed class AnswerKeyCorrectionConfiguration : IEntityTypeConfiguration<AnswerKeyCorrection>
{
    public void Configure(EntityTypeBuilder<AnswerKeyCorrection> builder)
    {
        builder.ConfigureEntity("AnswerKeyCorrections");
        builder.Property(c => c.OldKeyJson).IsRequired();
        builder.Property(c => c.NewKeyJson).IsRequired();
        builder.Property(c => c.Reason).HasMaxLength(1000).IsRequired();
        builder.HasOne<ExamQuestion>().WithMany().HasForeignKey(c => c.ExamQuestionId)
            .HasConstraintName("FK_AnswerKeyCorrections_Question");
        builder.HasUserReference(c => c.CorrectedBy, "FK_AnswerKeyCorrections_By");
        builder.HasIndex(c => c.ExamQuestionId).HasDatabaseName("IX_AnswerKeyCorrections_Question");
        builder.ToTable(t => t.HasEnumCheck<AnswerKeyCorrectionType>("CK_AnswerKeyCorrections_Type", "CorrectionType"));
    }
}
