using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence.Repositories;

public class VenueRepository(AppDbContext db) : IVenueRepository
{
    public Task<Venue?> GetByIdAsync(int id, CancellationToken ct) =>
        db.Venues.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<IReadOnlyList<Venue>> GetAllAsync(CancellationToken ct) =>
        await db.Venues.ToListAsync(ct);
}
