using MediatR;

namespace EventosVivos.Application.Reservations.Queries.GetReservation;

public record GetReservationQuery(Guid ReservationId) : IRequest<ReservationDetailDto>;

public record ReservationDetailDto(
    Guid   Id,
    Guid   EventId,
    string EventTitle,
    DateTime EventStartDateTime,
    string Status,
    int    Quantity,
    string BuyerName,
    string BuyerEmail,
    string? ReservationCode,
    bool   IsLost,
    DateTime CreatedAt,
    DateTime? CancelledAt
);
