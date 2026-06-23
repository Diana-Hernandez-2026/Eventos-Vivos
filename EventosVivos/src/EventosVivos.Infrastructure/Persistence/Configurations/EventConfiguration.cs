using EventosVivos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventosVivos.Infrastructure.Persistence.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
        builder.Property(e => e.TicketPrice).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Type).HasConversion<string>();
        builder.Property(e => e.Status).HasConversion<string>();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.Venue)
            .WithMany(v => v.Events)
            .HasForeignKey(e => e.VenueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.VenueId, e.StartDateTime, e.EndDateTime });
        builder.HasIndex(e => e.CreatedAt);

        builder.Ignore(e => e.ConfirmedTickets);
        builder.Ignore(e => e.LostTickets);
        builder.Ignore(e => e.AvailableTickets);
        builder.Ignore(e => e.IsActive);
    }
}
