using EventosVivos.Domain.Entities;
using EventosVivos.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Tests.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);

        db.Venues.AddRange(
            new Venue { Id = 1, Name = "Auditorio Central", Capacity = 200, City = "Bogotá" },
            new Venue { Id = 2, Name = "Sala Norte", Capacity = 50, City = "Bogotá" },
            new Venue { Id = 3, Name = "Arena Sur", Capacity = 500, City = "Medellín" }
        );
        db.SaveChanges();

        return db;
    }
}
