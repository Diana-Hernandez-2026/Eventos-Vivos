using EventosVivos.Domain.Enums;

namespace EventosVivos.Domain.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int VenueId { get; set; }
    public Venue Venue { get; set; } = null!;
    public int MaxCapacity { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public decimal TicketPrice { get; set; }
    public EventType Type { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Activo;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

    public int ConfirmedTickets =>
        Reservations.Where(r => r.Status == ReservationStatus.Confirmada).Sum(r => r.Quantity);

    public int LostTickets =>
        Reservations.Where(r => r.IsLost).Sum(r => r.Quantity);

    public int AvailableTickets =>
        MaxCapacity - ConfirmedTickets - LostTickets;

    public bool IsActive => Status == EventStatus.Activo;
}
