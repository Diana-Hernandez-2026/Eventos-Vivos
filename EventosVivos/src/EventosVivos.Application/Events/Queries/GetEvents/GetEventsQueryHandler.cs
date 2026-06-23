using EventosVivos.Application.Common;
using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Events.Queries.GetEvents;

public class GetEventsQueryHandler(IEventRepository eventRepo) : IRequestHandler<GetEventsQuery, CursorPage<EventDto>>
{
    public async Task<CursorPage<EventDto>> Handle(GetEventsQuery query, CancellationToken ct)
    {
        await eventRepo.UpdateCompletedStatusAsync(ct);

        var limit = Math.Clamp(query.Limit, 1, 100);
        var (items, nextCursor) = await eventRepo.GetPagedAsync(
            query.Type, query.StartFrom, query.StartTo,
            query.VenueId, query.Status, query.TitleSearch,
            query.Cursor, limit + 1, ct);

        var hasNext = items.Count > limit;
        var page = items.Take(limit).ToList();

        var dtos = page.Select(e => new EventDto(
            e.Id, e.Title, e.Description, e.VenueId,
            e.Venue?.Name ?? string.Empty,
            e.MaxCapacity, e.StartDateTime, e.EndDateTime,
            e.TicketPrice, e.Type.ToString(), e.Status.ToString(), e.CreatedAt
        )).ToList();

        var cursor = hasNext && page.Count > 0
            ? CursorEncoder.Encode(page[^1].CreatedAt, page[^1].Id)
            : null;

        return new CursorPage<EventDto>(dtos, cursor, hasNext, dtos.Count);
    }
}
