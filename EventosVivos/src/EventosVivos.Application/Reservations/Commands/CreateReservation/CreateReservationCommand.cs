using MediatR;

namespace EventosVivos.Application.Reservations.Commands.CreateReservation;

public record CreateReservationCommand(
    Guid EventId,
    int Quantity,
    string BuyerName,
    string BuyerEmail
) : IRequest<CreateReservationResult>;

public record CreateReservationResult(Guid ReservationId, string Status);
