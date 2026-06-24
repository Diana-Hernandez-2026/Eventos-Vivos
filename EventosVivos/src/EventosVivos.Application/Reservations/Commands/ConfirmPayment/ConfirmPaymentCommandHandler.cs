using EventosVivos.Application.Common;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Reservations.Commands.ConfirmPayment;

public class ConfirmPaymentCommandHandler(IReservationRepository reservationRepo)
    : IRequestHandler<ConfirmPaymentCommand, ConfirmPaymentResult>
{
    public async Task<ConfirmPaymentResult> Handle(ConfirmPaymentCommand cmd, CancellationToken ct)
    {
        var reservation = await reservationRepo.GetByIdAsync(cmd.ReservationId, ct)
            ?? throw new NotFoundException("Reservation", cmd.ReservationId);

        if (reservation.Status == ReservationStatus.Confirmada)
            throw new ConflictException(I18n.AlreadyConfirmed);

        if (reservation.Status == ReservationStatus.Cancelada)
            throw new DomainException(I18n.CannotConfirmCancelled);

        reservation.Status = ReservationStatus.Confirmada;
        reservation.ReservationCode = await GenerateUniqueCodeAsync(reservationRepo, ct);

        await reservationRepo.SaveChangesAsync(ct);

        return new ConfirmPaymentResult(reservation.Id, reservation.ReservationCode, reservation.Status.ToString());
    }

    private static async Task<string> GenerateUniqueCodeAsync(IReservationRepository repo, CancellationToken ct)
    {
        string code;
        do
        {
            code = $"EV-{Random.Shared.Next(100000, 999999)}";
        }
        while (await repo.ReservationCodeExistsAsync(code, ct));
        return code;
    }
}
