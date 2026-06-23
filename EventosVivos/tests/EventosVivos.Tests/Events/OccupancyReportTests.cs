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

    [Fact]
    public async Task GetOccupancyReport_WithNoReservations_ReturnsZeroValues()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var evt = new Event
        {
            Title = "Empty Event",
            Description = "No reservations",
            VenueId = 1,
            MaxCapacity = 100,
            StartDateTime = start,
            EndDateTime = start.AddHours(2),
            TicketPrice = 50,
            Type = EventType.Conferencia,
            Status = EventStatus.Activo
        };
        _db.Events.Add(evt);
        _db.SaveChanges();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        report.ConfirmedTickets.Should().Be(0);
        report.LostTickets.Should().Be(0);
        report.AvailableTickets.Should().Be(100);
        report.OccupancyPercentage.Should().Be(0);
        report.TotalRevenue.Should().Be(0);
    }

    [Fact]
    public async Task GetOccupancyReport_ShouldIgnorePendingReservations()
    {
        var evt = CreateEventWithReservations();
        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);
        report.ConfirmedTickets.Should().NotBe(35);
    }

    [Fact]
    public async Task GetOccupancyReport_CancelledWithoutPenalty_ShouldNotReduceAvailability()
    {
        var evt = CreateEventWithReservations();
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id,
            Quantity = 10,
            BuyerName = "E",
            BuyerEmail = "e@test.com",
            Status = ReservationStatus.Cancelada,
            IsLost = false
        });
        _db.SaveChanges();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        report.LostTickets.Should().Be(8);
        report.AvailableTickets.Should().Be(62);
    }

    [Fact]
    public async Task GetOccupancyReport_EventNotFound_ThrowsException()
    {
        var act = () => _handler.Handle(new GetOccupancyReportQuery(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task GetOccupancyReport_Percentage_ShouldBeCalculatedCorrectly()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var evt = new Event
        {
            Title = "Test",
            Description = "Test",
            VenueId = 1,
            MaxCapacity = 3,
            StartDateTime = start,
            EndDateTime = start.AddHours(2),
            TicketPrice = 100,
            Type = EventType.Conferencia,
            Status = EventStatus.Activo
        };

        _db.Events.Add(evt);
        _db.SaveChanges();
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id,
            Quantity = 1,
            BuyerName = "A",
            BuyerEmail = "a@test.com",
            Status = ReservationStatus.Confirmada
        });
        _db.SaveChanges();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        report.OccupancyPercentage.Should().BeApproximately(33.33m, 0.1m);
    }

    [Fact]
    public async Task GetOccupancyReport_FullyBooked_ShowsZeroAvailable()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var evt = new Event
        {
            Title = "Sold Out", Description = "Sold out event", VenueId = 1,
            MaxCapacity = 10, StartDateTime = start, EndDateTime = start.AddHours(2),
            TicketPrice = 50m, Type = EventType.Concierto, Status = EventStatus.Activo
        };
        _db.Events.Add(evt);
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id, Quantity = 10, BuyerName = "A", BuyerEmail = "a@t.com",
            Status = ReservationStatus.Confirmada
        });
        _db.SaveChanges();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        report.AvailableTickets.Should().Be(0);
        report.OccupancyPercentage.Should().Be(100m);
    }

    [Fact]
    public async Task GetOccupancyReport_RevenueExcludesCancelledAndPending()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var evt = new Event
        {
            Title = "Partial Revenue", Description = "Test revenue", VenueId = 1,
            MaxCapacity = 100, StartDateTime = start, EndDateTime = start.AddHours(2),
            TicketPrice = 100m, Type = EventType.Conferencia, Status = EventStatus.Activo
        };
        _db.Events.Add(evt);
        _db.Reservations.AddRange(
            new Reservation { EventId = evt.Id, Quantity = 5,  BuyerName = "A", BuyerEmail = "a@t.com", Status = ReservationStatus.Confirmada },
            new Reservation { EventId = evt.Id, Quantity = 10, BuyerName = "B", BuyerEmail = "b@t.com", Status = ReservationStatus.PendientePago },
            new Reservation { EventId = evt.Id, Quantity = 3,  BuyerName = "C", BuyerEmail = "c@t.com", Status = ReservationStatus.Cancelada }
        );
        _db.SaveChanges();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        // Only confirmed tickets (5) × price (100) = 500
        report.TotalRevenue.Should().Be(500m);
        report.ConfirmedTickets.Should().Be(5);
    }

    [Fact]
    public async Task GetOccupancyReport_MultipleLostReservations_SumsAllLostTickets()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var evt = new Event
        {
            Title = "Lost Tickets", Description = "Multiple lost reservations", VenueId = 1,
            MaxCapacity = 100, StartDateTime = start, EndDateTime = start.AddHours(2),
            TicketPrice = 60m, Type = EventType.Taller, Status = EventStatus.Activo
        };
        _db.Events.Add(evt);
        _db.Reservations.AddRange(
            new Reservation { EventId = evt.Id, Quantity = 4, BuyerName = "A", BuyerEmail = "a@t.com", Status = ReservationStatus.Cancelada, IsLost = true },
            new Reservation { EventId = evt.Id, Quantity = 6, BuyerName = "B", BuyerEmail = "b@t.com", Status = ReservationStatus.Cancelada, IsLost = true }
        );
        _db.SaveChanges();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        report.LostTickets.Should().Be(10);            // 4 + 6
        report.AvailableTickets.Should().Be(90);       // 100 - 0 confirmed - 10 lost
        report.TotalRevenue.Should().Be(0m);           // No confirmed tickets
    }

    [Fact]
    public async Task GetOccupancyReport_ReturnsCorrectEventMetadata()
    {
        var evt = CreateEventWithReservations();

        var report = await _handler.Handle(new GetOccupancyReportQuery(evt.Id), CancellationToken.None);

        report.EventId.Should().Be(evt.Id);
        report.Title.Should().Be("Music Concert");
        report.MaxCapacity.Should().Be(100);
    }

    public void Dispose() => _db.Dispose();
}
