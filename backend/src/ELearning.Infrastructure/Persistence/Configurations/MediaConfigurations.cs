using ELearning.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ELearning.Infrastructure.Persistence.Configurations;

internal sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ConfigureEntity("MediaFiles");
        builder.Property(m => m.Sha256).HasMaxLength(MediaFile.Sha256Length).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(m => m.OriginalFileName).HasMaxLength(MediaFile.MaxFileNameLength);
        builder.Ignore(m => m.StoragePath);

        builder.HasIndex(m => m.Sha256).IsUnique().HasDatabaseName("UQ_MediaFiles_Sha256");
        builder.HasUserReference(m => m.CreatedBy, "FK_MediaFiles_CreatedBy");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MediaFiles_ContentType", $"\"ContentType\" IN ({string.Join(",", MediaFile.AllowedContentTypes.Select(c => $"'{c}'"))})");
            t.HasCheckConstraint("CK_MediaFiles_Size", "\"SizeBytes\" > 0");
        });
    }
}
