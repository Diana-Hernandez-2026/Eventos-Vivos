using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Enums;

namespace EventosVivos.Domain.Interfaces;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Event?> GetByIdWithReservationsAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Event evt, CancellationToken ct = default);
    Task<bool> HasVenueOverlapAsync(int venueId, DateTime start, DateTime end, Guid? excludeEventId = null, CancellationToken ct = default);
    Task<(IReadOnlyList<Event> Items, string? NextCursor)> GetPagedAsync(
        EventType? type,
        DateTime? startFrom,
        DateTime? startTo,
        int? venueId,
        EventStatus? status,
        string? titleSearch,
        string? cursor,
        int limit,
        CancellationToken ct = default);
    Task UpdateCompletedStatusAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
