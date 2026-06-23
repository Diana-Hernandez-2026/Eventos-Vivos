using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Reservations.Commands.CancelReservation;

public class CancelReservationCommandHandler(
    IReservationRepository reservationRepo,
    IEventRepository eventRepo) : IRequestHandler<CancelReservationCommand, CancelReservationResult>
{
    public async Task<CancelReservationResult> Handle(CancelReservationCommand cmd, CancellationToken ct)
    {
        var reservation = await reservationRepo.GetByIdAsync(cmd.ReservationId, ct)
            ?? throw new NotFoundException("Reservation", cmd.ReservationId);

        if (reservation.Status == ReservationStatus.Cancelada)
            throw new ConflictException("Reservation is already cancelled.");

        if (reservation.Status == ReservationStatus.PendientePago)
            throw new DomainException("Cannot cancel a reservation with status 'pendiente_pago'. Only confirmed reservations can be cancelled.");

        var evt = await eventRepo.GetByIdAsync(reservation.EventId, ct)
            ?? throw new NotFoundException("Event", reservation.EventId);

        var hoursUntilEvent = (evt.StartDateTime - DateTime.UtcNow).TotalHours;

        // RN-07: penalty if cancelled within 48 hours
        var isLost = hoursUntilEvent < 48;

        reservation.Status = ReservationStatus.Cancelada;
        reservation.IsLost = isLost;
        reservation.CancelledAt = DateTime.UtcNow;

        await reservationRepo.SaveChangesAsync(ct);

        return new CancelReservationResult(reservation.Id, reservation.Status.ToString(), isLost, reservation.CancelledAt.Value);
    }
}
