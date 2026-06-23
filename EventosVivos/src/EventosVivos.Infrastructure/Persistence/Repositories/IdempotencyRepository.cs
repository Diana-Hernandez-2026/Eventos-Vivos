using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence.Repositories;

public class IdempotencyRepository(AppDbContext db) : IIdempotencyRepository
{
    public Task<IdempotencyRecord?> GetAsync(Guid key, CancellationToken ct) =>
        db.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Id == key && r.ExpiresAt > DateTime.UtcNow, ct);

    public async Task SaveAsync(IdempotencyRecord record, CancellationToken ct)
    {
        await db.IdempotencyRecords.AddAsync(record, ct);
        await db.SaveChangesAsync(ct);
    }
}
