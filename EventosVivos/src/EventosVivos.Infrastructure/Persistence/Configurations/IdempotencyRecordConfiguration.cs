using EventosVivos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventosVivos.Infrastructure.Persistence.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.RequestPath).IsRequired().HasMaxLength(500);
        builder.Property(i => i.RequestBodyHash).HasMaxLength(64);
        builder.Property(i => i.ResponseBody).IsRequired();
        builder.Property(i => i.ResponseContentType).HasMaxLength(100);
        builder.HasIndex(i => i.ExpiresAt);
    }
}
