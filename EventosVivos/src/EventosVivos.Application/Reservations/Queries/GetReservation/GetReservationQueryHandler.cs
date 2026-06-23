using EventosVivos.Domain.Exceptions;
using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Reservations.Queries.GetReservation;

public class GetReservationQueryHandler(IReservationRepository reservationRepo)
    : IRequestHandler<GetReservationQuery, ReservationDetailDto>
{
    public async Task<ReservationDetailDto> Handle(GetReservationQuery query, CancellationToken ct)
    {
        var r = await reservationRepo.GetByIdAsync(query.ReservationId, ct)
            ?? throw new NotFoundException("Reservation", query.ReservationId);

        return new ReservationDetailDto(
            r.Id,
            r.EventId,
            r.Event.Title,
            r.Event.StartDateTime,
            r.Status.ToString(),
            r.Quantity,
            r.BuyerName,
            r.BuyerEmail,
            r.ReservationCode,
            r.IsLost,
            r.CreatedAt,
            r.CancelledAt);
    }
}
