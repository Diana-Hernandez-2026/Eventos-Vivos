using MediatR;

namespace EventosVivos.Application.Reservations.Commands.ConfirmPayment;

public record ConfirmPaymentCommand(Guid ReservationId) : IRequest<ConfirmPaymentResult>;

public record ConfirmPaymentResult(Guid ReservationId, string ReservationCode, string Status);
