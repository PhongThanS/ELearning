using ELearning.Domain.Enums;
using ELearning.Domain.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal sealed class QuestionCategoryConfiguration : IEntityTypeConfiguration<QuestionCategory>
{
    public void Configure(EntityTypeBuilder<QuestionCategory> builder)
    {
        builder.ConfigureEntity("QuestionCategories");
        builder.Property(c => c.Code).IsCode(100);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Code).IsUnique().HasDatabaseName("UQ_QuestionCategories_Code");
        builder.HasUserReference(c => c.CreatedBy, "FK_QuestionCategories_CreatedBy");
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ConfigureEntity("Questions");
        builder.Property(q => q.Code).IsCode(100);
        builder.Property(q => q.Content).IsRequired();
        builder.Property(q => q.CorrectAnswerNumber).HasColumnType(ConfigurationExtensions.SqlDecimalAnswer);
        builder.Property(q => q.NumericTolerance).HasColumnType(ConfigurationExtensions.SqlDecimalAnswer);

        builder.HasIndex(q => q.Code).IsUnique().HasDatabaseName("UQ_Questions_Code");
        builder.HasIndex(q => new { q.CategoryId, q.QuestionType })
            .IncludeProperties(q => q.IsActive)
            .HasDatabaseName("IX_Questions_Category_Type");

        builder.HasOne<QuestionCategory>().WithMany().HasForeignKey(q => q.CategoryId)
            .HasConstraintName("FK_Questions_Category");
        builder.HasUserReference(q => q.CreatedBy, "FK_Questions_CreatedBy");
        builder.HasUserReference(q => q.UpdatedBy, "FK_Questions_UpdatedBy");

        builder.HasMany(q => q.Options).WithOne().HasForeignKey(o => o.QuestionId)
            .HasConstraintName("FK_QuestionOptions_Question").OnDelete(DeleteBehavior.ClientCascade);
        builder.HasMany(q => q.AcceptedAnswers).WithOne().HasForeignKey(a => a.QuestionId)
            .HasConstraintName("FK_QuestionAcceptedAnswers_Question").OnDelete(DeleteBehavior.ClientCascade);
        builder.HasMany(q => q.Tags).WithOne().HasForeignKey(t => t.QuestionId)
            .HasConstraintName("FK_QuestionTags_Question").OnDelete(DeleteBehavior.ClientCascade);

        builder.ToTable(t =>
        {
            t.HasEnumCheck<ContentFormat>("CK_Questions_ContentFormat", "ContentFormat");
            t.HasEnumCheck<QuestionDifficulty>("CK_Questions_Difficulty", "Difficulty");
            t.HasEnumCheck<QuestionType>("CK_Questions_Type", "QuestionType");
            t.HasEnumCheck<AnswerDataType>("CK_Questions_AnswerDataType", "AnswerDataType");
            t.HasCheckConstraint(
                "CK_Questions_FillIn",
                "(\"QuestionType\" = 'FILL_IN' AND \"AnswerDataType\" IS NOT NULL) OR (\"QuestionType\" <> 'FILL_IN' AND \"AnswerDataType\" IS NULL)");
            t.HasCheckConstraint(
                "CK_Questions_Number",
                "\"AnswerDataType\" IS NULL OR \"AnswerDataType\" <> 'NUMBER' OR (\"CorrectAnswerNumber\" IS NOT NULL AND \"NumericTolerance\" IS NOT NULL AND \"NumericTolerance\" >= 0)");
            t.HasCheckConstraint("CK_Questions_Score", "\"DefaultScore\" > 0");
        });
    }
}

internal sealed class QuestionOptionConfiguration : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ConfigureEntity("QuestionOptions");
        builder.Property(o => o.OptionCode).IsCode(10);
        builder.Property(o => o.Content).HasMaxLength(2000).IsRequired();
        builder.HasIndex(o => new { o.QuestionId, o.OptionCode }).IsUnique().HasDatabaseName("UQ_QuestionOptions_Code");
    }
}

internal sealed class QuestionTagConfiguration : IEntityTypeConfiguration<QuestionTag>
{
    public void Configure(EntityTypeBuilder<QuestionTag> builder)
    {
        builder.ConfigureEntity("QuestionTags");
        builder.Property(t => t.Tag).HasMaxLength(Question.MaxTagLength).IsRequired();
        builder.HasIndex(t => new { t.QuestionId, t.Tag }).IsUnique().HasDatabaseName("UQ_QuestionTags_Tag");
        builder.HasIndex(t => t.Tag).HasDatabaseName("IX_QuestionTags_Tag");
    }
}

internal sealed class QuestionAcceptedAnswerConfiguration : IEntityTypeConfiguration<QuestionAcceptedAnswer>
{
    public void Configure(EntityTypeBuilder<QuestionAcceptedAnswer> builder)
    {
        builder.ConfigureEntity("QuestionAcceptedAnswers");
        builder.Property(a => a.AnswerText).HasMaxLength(1000).IsRequired();
        builder.HasIndex(a => new { a.QuestionId, a.DisplayOrder }).IsUnique()
            .HasDatabaseName("UQ_QuestionAcceptedAnswers_Order");
    }
}
