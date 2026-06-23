using EventosVivos.Domain.Enums;

namespace EventosVivos.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public int Quantity { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; } = ReservationStatus.PendientePago;
    public string? ReservationCode { get; set; }
    public bool IsLost { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
}
