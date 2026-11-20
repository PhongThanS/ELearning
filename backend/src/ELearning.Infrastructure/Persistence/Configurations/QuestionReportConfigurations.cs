using ELearning.Domain.Attempts;
using ELearning.Domain.Enums;
using ELearning.Domain.Exams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

/// <summary>Học sinh báo câu hỏi lỗi sau khi nộp bài (D-46, docs/04b-csdl-luot-thi-ket-qua.md mục 3c).</summary>
internal sealed class QuestionReportConfiguration : IEntityTypeConfiguration<QuestionReport>
{
    public void Configure(EntityTypeBuilder<QuestionReport> builder)
    {
        builder.ConfigureEntity("QuestionReports");
        builder.Property(r => r.Note).HasMaxLength(QuestionReport.MaxNoteLength);
        builder.Property(r => r.ResolutionNote).HasMaxLength(QuestionReport.MaxNoteLength);
        builder.Ignore(r => r.IsOpen);

        builder.HasOne<ExamAttempt>().WithMany().HasForeignKey(r => r.AttemptId).HasConstraintName("FK_QuestionReports_Attempt");
        builder.HasOne<Exam>().WithMany().HasForeignKey(r => r.ExamId).HasConstraintName("FK_QuestionReports_Exam");
        builder.HasOne<ExamQuestion>().WithMany().HasForeignKey(r => r.ExamQuestionId).HasConstraintName("FK_QuestionReports_ExamQuestion");
        builder.HasUserReference(r => r.ReportedBy, "FK_QuestionReports_ReportedBy");
        builder.HasUserReference(r => r.ResolvedBy, "FK_QuestionReports_ResolvedBy");

        // Mỗi lượt thi báo một câu tối đa một lần
        builder.HasIndex(r => new { r.AttemptId, r.ExamQuestionId }).IsUnique().HasDatabaseName("UQ_QuestionReports_Attempt_Question");
        // Danh sách câu bị báo lỗi (gom theo câu, lọc trạng thái)
        builder.HasIndex(r => new { r.Status, r.ExamQuestionId }).HasDatabaseName("IX_QuestionReports_Status_Question");
        builder.HasIndex(r => r.ExamQuestionId).HasDatabaseName("IX_QuestionReports_ExamQuestion");
        builder.HasIndex(r => r.ReportedBy).HasDatabaseName("IX_QuestionReports_ReportedBy");
        builder.ToTable(t =>
        {
            t.HasEnumCheck<QuestionReportReason>("CK_QuestionReports_Reason", "Reason");
            t.HasEnumCheck<QuestionReportStatus>("CK_QuestionReports_Status", "Status");
        });
    }
}
