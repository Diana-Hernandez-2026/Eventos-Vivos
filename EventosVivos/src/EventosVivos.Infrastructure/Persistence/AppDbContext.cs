using EventosVivos.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EventosVivos.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyUtcDateTimeConverters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    // SQLite stores DateTime as text with no timezone info. EF Core reads them back as
    // Kind=Unspecified, which System.Text.Json serializes without the 'Z' suffix.
    // JavaScript then treats the timestamp as local time instead of UTC, causing a
    // double-offset shift on the client. This converter tags every DateTime read from
    // SQLite as UTC so the 'Z' is always present in the JSON response.
    private static void ApplyUtcDateTimeConverters(ModelBuilder modelBuilder)
    {
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            write => write.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(write, DateTimeKind.Utc)
                : write.ToUniversalTime(),
            read  => DateTime.SpecifyKind(read, DateTimeKind.Utc));

        var utcNullConverter = new ValueConverter<DateTime?, DateTime?>(
            write => write.HasValue
                ? (write.Value.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(write.Value, DateTimeKind.Utc)
                    : write.Value.ToUniversalTime())
                : write,
            read  => read.HasValue
                ? DateTime.SpecifyKind(read.Value, DateTimeKind.Utc)
                : read);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(utcNullConverter);
            }
        }
    }
}
