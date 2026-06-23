using EventosVivos.Application.Events.Queries.GetOccupancyReport;
using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Enums;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Repositories;
using EventosVivos.Tests.Helpers;
using FluentAssertions;

namespace EventosVivos.Tests.Events;

public class OccupancyReportTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GetOccupancyReportQueryHandler _handler;

    public OccupancyReportTests()
    {
        _db = TestDbContextFactory.Create();
        _handler = new GetOccupancyReportQueryHandler(new EventRepository(_db));
    }

    private Event CreateEventWithReservations()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var evt = new Event
        {
            Title = "Music Concert",
            Description = "A great music event",
            VenueId = 1,
            MaxCapacity = 100,
            StartDateTime = start,
            EndDateTime = start.AddHours(3),
            TicketPrice = 75m,
            Type = EventType.Concierto,
            Status = EventStatus.Activo
        };
        _db.Events.Add(evt);
        _db.SaveChanges();

        _db.Reservations.AddRange(
            new Reservation { EventId = evt.Id, Quantity = 20, BuyerName = "A", BuyerEmail = "a@t.com", Status = ReservationStatus.Confirmada },
            new Reservation { EventId = evt.Id, Quantity = 10, BuyerName = "B", BuyerEmail = "b@t.com", Status = ReservationStatus.Confirmada },
            new Reservation { EventId = evt.Id, Quantity = 5, BuyerName = "C", BuyerEmail = "c@t.com", Status = ReservationStatus.PendientePago },
            new Reservation { EventId = evt.Id, Quantity = 8, BuyerName = "D", BuyerEmail = "d@t.com", Status = ReservationStatus.Cancelada, IsLost = true }
        );
        _db.SaveChanges();

        return evt;
    }

    [Fact]
    public async Task GetOccupancyReport_ReturnsCorrectCounts()
    {
        var evt = CreateEventWithReservations();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        report.ConfirmedTickets.Should().Be(30);    // 20 + 10
        report.LostTickets.Should().Be(8);           // cancelled with IsLost
        report.AvailableTickets.Should().Be(62);     // 100 - 30 - 8
        report.OccupancyPercentage.Should().Be(30m); // 30/100 * 100
        report.TotalRevenue.Should().Be(30 * 75m);   // 2250
    }

    public void Dispose() => _db.Dispose();
}
