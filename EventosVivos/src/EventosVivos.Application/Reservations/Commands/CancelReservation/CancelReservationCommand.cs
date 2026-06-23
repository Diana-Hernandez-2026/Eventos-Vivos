using MediatR;

namespace EventosVivos.Application.Reservations.Commands.CancelReservation;

public record CancelReservationCommand(Guid ReservationId) : IRequest<CancelReservationResult>;

public record CancelReservationResult(Guid ReservationId, string Status, bool IsLost, DateTime CancelledAt);
