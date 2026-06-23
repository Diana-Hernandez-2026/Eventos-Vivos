using EventosVivos.Domain.Enums;
using MediatR;

namespace EventosVivos.Application.Events.Commands.CreateEvent;

public record CreateEventCommand(
    string Title,
    string Description,
    int VenueId,
    int MaxCapacity,
    DateTime StartDateTime,
    DateTime EndDateTime,
    decimal TicketPrice,
    EventType Type
) : IRequest<CreateEventResult>;

public record CreateEventResult(Guid Id, string Title, EventStatus Status);
