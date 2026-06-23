using EventosVivos.Domain.Entities;

namespace EventosVivos.Domain.Interfaces;

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetAsync(Guid key, CancellationToken ct = default);
    Task SaveAsync(IdempotencyRecord record, CancellationToken ct = default);
}
