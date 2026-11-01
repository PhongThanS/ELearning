using ELearning.Domain.Classes;
using ELearning.Domain.Questions;
using ELearning.Domain.Videos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal sealed class VideoLessonConfiguration : IEntityTypeConfiguration<VideoLesson>
{
    public void Configure(EntityTypeBuilder<VideoLesson> builder)
    {
        builder.ConfigureEntity("VideoLessons");
        builder.Property(v => v.Title).HasMaxLength(VideoLesson.MaxTitleLength).IsRequired();
        builder.Property(v => v.VideoUrl).HasMaxLength(VideoLesson.MaxUrlLength).IsRequired();
        builder.Property(v => v.DocumentUrl).HasMaxLength(VideoLesson.MaxUrlLength);
        builder.Property(v => v.YoutubeVideoId).HasMaxLength(50).IsUnicode(false);
        builder.Property(v => v.ThumbnailUrl).HasMaxLength(VideoLesson.MaxUrlLength);
        builder.Property(v => v.Description).HasMaxLength(VideoLesson.MaxDescriptionLength);

        builder.HasOne(v => v.Category)
            .WithMany()
            .HasForeignKey(v => v.CategoryId)
            .HasConstraintName("FK_VideoLessons_Category");

        builder.HasMany(v => v.Classrooms)
            .WithOne()
            .HasForeignKey(c => c.VideoLessonId)
            .HasConstraintName("FK_VideoLessonClassrooms_VideoLesson")
            .OnDelete(DeleteBehavior.ClientCascade);

        builder.HasUserReference(v => v.CreatedBy, "FK_VideoLessons_CreatedBy");
        builder.HasUserReference(v => v.UpdatedBy, "FK_VideoLessons_UpdatedBy");

        builder.HasIndex(v => v.CategoryId).HasDatabaseName("IX_VideoLessons_CategoryId");
    }
}

internal sealed class VideoLessonClassroomConfiguration : IEntityTypeConfiguration<VideoLessonClassroom>
{
    public void Configure(EntityTypeBuilder<VideoLessonClassroom> builder)
    {
        builder.ToTable("VideoLessonClassrooms");
        builder.HasKey(c => new { c.VideoLessonId, c.ClassroomId }).HasName("PK_VideoLessonClassrooms");
        builder.HasOne<Classroom>().WithMany().HasForeignKey(c => c.ClassroomId).HasConstraintName("FK_VideoLessonClassrooms_Classroom");
        // Lọc video theo lớp, kiểm tra lớp còn được gán video trước khi xóa lớp
        builder.HasIndex(c => c.ClassroomId).HasDatabaseName("IX_VideoLessonClassrooms_Classroom");
    }
}
