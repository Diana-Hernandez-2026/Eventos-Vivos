using EventosVivos.Application.Common;
using EventosVivos.Domain.Enums;
using MediatR;

namespace EventosVivos.Application.Events.Queries.GetEvents;

public record GetEventsQuery(
    EventType? Type,
    DateTime? StartFrom,
    DateTime? StartTo,
    int? VenueId,
    EventStatus? Status,
    string? TitleSearch,
    string? Cursor,
    int Limit = 20
) : IRequest<CursorPage<EventDto>>;

public record EventDto(
    Guid Id,
    string Title,
    string Description,
    int VenueId,
    string VenueName,
    int MaxCapacity,
    DateTime StartDateTime,
    DateTime EndDateTime,
    decimal TicketPrice,
    string Type,
    string Status,
    DateTime CreatedAt
);
