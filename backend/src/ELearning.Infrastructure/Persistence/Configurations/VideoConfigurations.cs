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
        builder.Property(v => v.YoutubeVideoId).HasMaxLength(50).IsUnicode(false);
        builder.Property(v => v.ThumbnailUrl).HasMaxLength(VideoLesson.MaxUrlLength);
        builder.Property(v => v.Description).HasMaxLength(VideoLesson.MaxDescriptionLength);

        builder.HasOne(v => v.Category)
            .WithMany()
            .HasForeignKey(v => v.CategoryId)
            .HasConstraintName("FK_VideoLessons_Category");

        builder.HasOne(v => v.Classroom)
            .WithMany()
            .HasForeignKey(v => v.ClassroomId)
            .HasConstraintName("FK_VideoLessons_Classroom");

        builder.HasUserReference(v => v.CreatedBy, "FK_VideoLessons_CreatedBy");
        builder.HasUserReference(v => v.UpdatedBy, "FK_VideoLessons_UpdatedBy");

        builder.HasIndex(v => v.CategoryId).HasDatabaseName("IX_VideoLessons_CategoryId");
        builder.HasIndex(v => v.ClassroomId).HasDatabaseName("IX_VideoLessons_ClassroomId");
    }
}
