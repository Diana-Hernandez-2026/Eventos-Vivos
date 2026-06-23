using EventosVivos.Application.Reservations.Queries.GetReservation;
using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Repositories;
using EventosVivos.Tests.Helpers;
using FluentAssertions;

namespace EventosVivos.Tests.Reservations;

public class GetReservationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly GetReservationQueryHandler _handler;

    public GetReservationTests()
    {
        _db = TestDbContextFactory.Create();
        _handler = new GetReservationQueryHandler(new ReservationRepository(_db));
    }

    private (Event evt, Reservation reservation) Seed(
        ReservationStatus status = ReservationStatus.Confirmada,
        string? code = "EV-999001",
        bool isLost = false,
        DateTime? cancelledAt = null,
        double eventDaysFromNow = 5)
    {
        var start = DateTime.UtcNow.AddDays(eventDaysFromNow);
        var evt = new Event
        {
            Title       = "Conferencia Tecnológica",
            Description = "Gran conferencia de tecnología",
            VenueId     = 1,
            MaxCapacity = 100,
            StartDateTime = start,
            EndDateTime   = start.AddHours(4),
            TicketPrice = 80m,
            Type   = EventType.Conferencia,
            Status = EventStatus.Activo
        };
        _db.Events.Add(evt);

        var reservation = new Reservation
        {
            EventId         = evt.Id,
            Quantity        = 3,
            BuyerName       = "María López",
            BuyerEmail      = "maria@test.com",
            Status          = status,
            ReservationCode = code,
            IsLost          = isLost,
            CancelledAt     = cancelledAt
        };
        _db.Reservations.Add(reservation);
        _db.SaveChanges();
        return (evt, reservation);
    }

    [Fact]
    public async Task GetReservation_WithValidId_ReturnsCorrectFields()
    {
        var (evt, reservation) = Seed();

        var result = await _handler.Handle(new GetReservationQuery(reservation.Id), CancellationToken.None);

        result.Id.Should().Be(reservation.Id);
        result.EventId.Should().Be(evt.Id);
        result.EventTitle.Should().Be("Conferencia Tecnológica");
        result.Quantity.Should().Be(3);
        result.BuyerName.Should().Be("María López");
        result.BuyerEmail.Should().Be("maria@test.com");
    }

    [Fact]
    public async Task GetReservation_WithNonExistentId_ThrowsNotFoundException()
    {
        var act = () => _handler.Handle(new GetReservationQuery(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetReservation_ConfirmedReservation_HasCodeAndCorrectStatus()
    {
        var (_, reservation) = Seed(ReservationStatus.Confirmada, code: "EV-123456");

        var result = await _handler.Handle(new GetReservationQuery(reservation.Id), CancellationToken.None);

        result.Status.Should().Be("Confirmada");
        result.ReservationCode.Should().Be("EV-123456");
        result.IsLost.Should().BeFalse();
    }

    [Fact]
    public async Task GetReservation_PendingReservation_HasNullCodeAndPendingStatus()
    {
        var (_, reservation) = Seed(ReservationStatus.PendientePago, code: null);

        var result = await _handler.Handle(new GetReservationQuery(reservation.Id), CancellationToken.None);

        result.Status.Should().Be("PendientePago");
        result.ReservationCode.Should().BeNull();
    }

    [Fact]
    public async Task GetReservation_CancelledWithPenalty_IsLostTrueAndCancelledAtSet()
    {
        var cancelledAt = DateTime.UtcNow.AddMinutes(-10);
        var (_, reservation) = Seed(
            ReservationStatus.Cancelada,
            code: null,
            isLost: true,
            cancelledAt: cancelledAt);

        var result = await _handler.Handle(new GetReservationQuery(reservation.Id), CancellationToken.None);

        result.IsLost.Should().BeTrue();
        result.Status.Should().Be("Cancelada");
        result.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetReservation_IncludesEventStartDateTime()
    {
        var (evt, reservation) = Seed();

        var result = await _handler.Handle(new GetReservationQuery(reservation.Id), CancellationToken.None);

        result.EventStartDateTime.Should().BeCloseTo(evt.StartDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetReservation_CreatedAtIsPopulated()
    {
        var (_, reservation) = Seed();

        var result = await _handler.Handle(new GetReservationQuery(reservation.Id), CancellationToken.None);

        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetReservation_CancelledWithoutPenalty_IsLostFalse()
    {
        var (_, reservation) = Seed(
            ReservationStatus.Cancelada,
            code: null,
            isLost: false,
            cancelledAt: DateTime.UtcNow.AddMinutes(-2));

        var result = await _handler.Handle(new GetReservationQuery(reservation.Id), CancellationToken.None);

        result.IsLost.Should().BeFalse();
        result.Status.Should().Be("Cancelada");
    }

    public void Dispose() => _db.Dispose();
}
