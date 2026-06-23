using EventosVivos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence.Seeding;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Venues.AnyAsync()) return;

        db.Venues.AddRange(
            new Venue { Id = 1, Name = "Auditorio Central", Capacity = 200, City = "Bogotá" },
            new Venue { Id = 2, Name = "Sala Norte", Capacity = 50, City = "Bogotá" },
            new Venue { Id = 3, Name = "Arena Sur", Capacity = 500, City = "Medellín" }
        );

        await db.SaveChangesAsync();
    }
}
