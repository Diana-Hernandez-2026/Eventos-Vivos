using EventosVivos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventosVivos.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.BuyerName).IsRequired().HasMaxLength(200);
        builder.Property(r => r.BuyerEmail).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Status).HasConversion<string>();
        builder.Property(r => r.ReservationCode).HasMaxLength(20);

        builder.HasOne(r => r.Event)
            .WithMany(e => e.Reservations)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.ReservationCode).IsUnique().HasFilter("[ReservationCode] IS NOT NULL");
        builder.HasIndex(r => r.EventId);
    }
}
