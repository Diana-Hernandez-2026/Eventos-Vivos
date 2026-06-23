using EventosVivos.Application.Common;
using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventosVivos.Infrastructure.Persistence.Repositories;

public class EventRepository(AppDbContext db) : IEventRepository
{
    public Task<Event?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Events.Include(e => e.Venue).FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Event?> GetByIdWithReservationsAsync(Guid id, CancellationToken ct) =>
        db.Events
            .Include(e => e.Venue)
            .Include(e => e.Reservations)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(Event evt, CancellationToken ct) =>
        await db.Events.AddAsync(evt, ct);

    public Task<bool> HasVenueOverlapAsync(int venueId, DateTime start, DateTime end, Guid? excludeEventId, CancellationToken ct) =>
        db.Events.AnyAsync(e =>
            e.VenueId == venueId &&
            e.Status == EventStatus.Activo &&
            (excludeEventId == null || e.Id != excludeEventId) &&
            e.StartDateTime < end && e.EndDateTime > start, ct);

    public async Task<(IReadOnlyList<Event> Items, string? NextCursor)> GetPagedAsync(
        EventType? type, DateTime? startFrom, DateTime? startTo,
        int? venueId, EventStatus? status, string? titleSearch,
        string? cursor, int limit, CancellationToken ct)
    {
        var query = db.Events.Include(e => e.Venue).AsQueryable();

        if (type.HasValue)
            query = query.Where(e => e.Type == type.Value);
        if (startFrom.HasValue)
            query = query.Where(e => e.StartDateTime >= startFrom.Value);
        if (startTo.HasValue)
            query = query.Where(e => e.StartDateTime <= startTo.Value);
        if (venueId.HasValue)
            query = query.Where(e => e.VenueId == venueId.Value);
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(titleSearch))
            query = query.Where(e => e.Title.ToLower().Contains(titleSearch.ToLower()));

        // Cursor-based pagination: order by CreatedAt ASC, Id ASC
        if (cursor is not null)
        {
            var decoded = CursorEncoder.Decode(cursor);
            if (decoded is not null)
            {
                var (cursorDate, cursorId) = decoded.Value;
                query = query.Where(e =>
                    e.CreatedAt > cursorDate ||
                    (e.CreatedAt == cursorDate && e.Id.CompareTo(cursorId) > 0));
            }
        }

        var items = await query
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .Take(limit)
            .ToListAsync(ct);

        return (items, null);
    }

    public async Task UpdateCompletedStatusAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var toComplete = await db.Events
            .Where(e => e.Status == EventStatus.Activo && e.EndDateTime < now)
            .ToListAsync(ct);

        foreach (var e in toComplete)
            e.Status = EventStatus.Completado;

        if (toComplete.Count > 0)
            await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
