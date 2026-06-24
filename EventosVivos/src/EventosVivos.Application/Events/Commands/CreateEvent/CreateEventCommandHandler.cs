using EventosVivos.Application.Common;
using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Events.Commands.CreateEvent;

public class CreateEventCommandHandler(
    IEventRepository eventRepo,
    IVenueRepository venueRepo,
    IBusinessClock clock) : IRequestHandler<CreateEventCommand, CreateEventResult>
{
    public async Task<CreateEventResult> Handle(CreateEventCommand cmd, CancellationToken ct)
    {
        var venue = await venueRepo.GetByIdAsync(cmd.VenueId, ct)
            ?? throw new NotFoundException(nameof(Venue), cmd.VenueId);

        // RN-01: capacity cannot exceed venue capacity
        if (cmd.MaxCapacity > venue.Capacity)
            throw new DomainException(I18n.Rn01(cmd.MaxCapacity, venue.Capacity));

        // RN-02: no overlapping events at same venue
        var hasOverlap = await eventRepo.HasVenueOverlapAsync(cmd.VenueId, cmd.StartDateTime, cmd.EndDateTime, null, ct);
        if (hasOverlap)
            throw new DomainException(I18n.Rn02);

        // RN-03: weekend events cannot start strictly after 22:00 (22:00 itself is allowed)
        var localStart = clock.ToBusinessLocal(cmd.StartDateTime);
        if (localStart.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            && localStart.TimeOfDay > TimeSpan.FromHours(22))
            throw new DomainException(I18n.Rn03);

        var evt = new Event
        {
            Title = cmd.Title,
            Description = cmd.Description,
            VenueId = cmd.VenueId,
            MaxCapacity = cmd.MaxCapacity,
            StartDateTime = cmd.StartDateTime,
            EndDateTime = cmd.EndDateTime,
            TicketPrice = cmd.TicketPrice,
            Type = cmd.Type
        };

        await eventRepo.AddAsync(evt, ct);
        await eventRepo.SaveChangesAsync(ct);

        return new CreateEventResult(evt.Id, evt.Title, evt.Status);
    }
}
