using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Reservations.Commands.CreateReservation;

public class CreateReservationCommandHandler(
    IEventRepository eventRepo,
    IReservationRepository reservationRepo) : IRequestHandler<CreateReservationCommand, CreateReservationResult>
{
    public async Task<CreateReservationResult> Handle(CreateReservationCommand cmd, CancellationToken ct)
    {
        var evt = await eventRepo.GetByIdWithReservationsAsync(cmd.EventId, ct)
            ?? throw new NotFoundException("Event", cmd.EventId);

        if (!evt.IsActive)
            throw new DomainException($"Cannot reserve tickets for an event with status '{evt.Status}'.");

        var hoursUntilStart = (evt.StartDateTime - DateTime.UtcNow).TotalHours;

        // RN-04: no reservations within 1 hour of start
        if (hoursUntilStart < 1)
            throw new DomainException("Reservations are not allowed for events starting in less than 1 hour.");

        // RN-03 for reservations: determine max quantity
        // RF-03 rule: <24h → max 5, takes priority over RN-05
        int maxAllowed;
        if (hoursUntilStart < 24)
        {
            maxAllowed = 5;
        }
        else if (evt.TicketPrice > 100)
        {
            // RN-05: price > $100 → max 10
            maxAllowed = 10;
        }
        else
        {
            maxAllowed = int.MaxValue;
        }

        if (cmd.Quantity > maxAllowed)
            throw new DomainException($"Maximum {maxAllowed} tickets allowed per transaction for this event.");

        // Check available tickets
        var available = evt.AvailableTickets;
        if (cmd.Quantity > available)
            throw new DomainException($"Only {available} tickets available.");

        var reservation = new Reservation
        {
            EventId = cmd.EventId,
            Quantity = cmd.Quantity,
            BuyerName = cmd.BuyerName,
            BuyerEmail = cmd.BuyerEmail
        };

        await reservationRepo.AddAsync(reservation, ct);
        await reservationRepo.SaveChangesAsync(ct);

        return new CreateReservationResult(reservation.Id, reservation.Status.ToString());
    }
}
