using EventosVivos.Application.Reservations.Commands.CreateReservation;
using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Repositories;
using EventosVivos.Tests.Helpers;
using FluentAssertions;

namespace EventosVivos.Tests.Reservations;

public class CreateReservationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateReservationCommandHandler _handler;

    public CreateReservationTests()
    {
        _db = TestDbContextFactory.Create();
        _handler = new CreateReservationCommandHandler(
            new EventRepository(_db),
            new ReservationRepository(_db));
    }

    private Event CreateActiveEvent(int capacity = 100, decimal price = 50m, double hoursFromNow = 48)
    {
        var start = DateTime.UtcNow.AddHours(hoursFromNow);
        var evt = new Event
        {
            Title = "Test Event",
            Description = "Test description for event",
            VenueId = 1,
            MaxCapacity = capacity,
            StartDateTime = start,
            EndDateTime = start.AddHours(4),
            TicketPrice = price,
            Type = EventType.Conferencia,
            Status = EventStatus.Activo
        };
        _db.Events.Add(evt);
        _db.SaveChanges();
        return evt;
    }

    [Fact]
    public async Task CreateReservation_WithValidData_Succeeds()
    {
        var evt = CreateActiveEvent();
        var cmd = new CreateReservationCommand(evt.Id, 2, "Juan Pérez", "juan@test.com");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be("PendientePago");
        result.ReservationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateReservation_ForNonExistentEvent_ThrowsNotFoundException()
    {
        var cmd = new CreateReservationCommand(Guid.NewGuid(), 1, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateReservation_WhenNoTicketsAvailable_ThrowsDomainException()
    {
        var evt = CreateActiveEvent(capacity: 2);
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id,
            Quantity = 2,
            BuyerName = "Maria",
            BuyerEmail = "maria@test.com",
            Status = ReservationStatus.Confirmada
        });
        _db.SaveChanges();

        var cmd = new CreateReservationCommand(evt.Id, 1, "Pedro", "pedro@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*available*");
    }

    // Note: email/quantity format validation is covered by CreateReservationCommandValidator tests.
    // The handler enforces business rules only (capacity, timing, status, etc.).

    [Fact]
    public async Task CreateReservation_PriceOver100_AllowsExactly10Tickets()
    {
        var evt = CreateActiveEvent(price: 150m, hoursFromNow: 72);
        var cmd = new CreateReservationCommand(evt.Id, 10, "Juan", "juan@test.com");
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReservation_WhenExceedingRemainingCapacity_ThrowsException()
    {
        var evt = CreateActiveEvent(capacity: 10);
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id,
            Quantity = 8,
            BuyerName = "Maria",
            BuyerEmail = "maria@test.com",
            Status = ReservationStatus.Confirmada
        });
        _db.SaveChanges();

        var cmd = new CreateReservationCommand(evt.Id, 3, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateReservation_PendingReservations_ShouldNotBlockCapacity()
    {
        var evt = CreateActiveEvent(capacity: 10);
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id,
            Quantity = 10,
            BuyerName = "Maria",
            BuyerEmail = "maria@test.com",
            Status = ReservationStatus.PendientePago
        });
        _db.SaveChanges();

        var cmd = new CreateReservationCommand(evt.Id, 5, "Juan", "juan@test.com");
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReservation_ForCompletedEvent_ThrowsException()
    {
        var evt = CreateActiveEvent();
        evt.Status = EventStatus.Completado;
        _db.SaveChanges();

        var cmd = new CreateReservationCommand(evt.Id, 1, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateReservation_WithinOneHourOfStart_ThrowsDomainException()
    {
        // Event starts in 30 minutes
        var evt = CreateActiveEvent(hoursFromNow: 0.5);

        var cmd = new CreateReservationCommand(evt.Id, 1, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*1 hour*");
    }

    [Fact]
    public async Task CreateReservation_PriceOver100_LimitedTo10Tickets()
    {
        var evt = CreateActiveEvent(capacity: 100, price: 150m, hoursFromNow: 72);

        var cmd = new CreateReservationCommand(evt.Id, 11, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*10*");
    }

    [Fact]
    public async Task CreateReservation_Within24hOfStart_LimitedTo5Tickets()
    {
        // Event starts in 12 hours (under 24h, over 1h)
        var evt = CreateActiveEvent(capacity: 100, price: 150m, hoursFromNow: 12);

        // The <24h rule (max 5) overrides the price>100 rule (max 10)
        var cmd = new CreateReservationCommand(evt.Id, 6, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*5*");
    }

    [Fact]
    public async Task CreateReservation_Within24hOfStart_AllowsUpTo5()
    {
        var evt = CreateActiveEvent(capacity: 100, price: 150m, hoursFromNow: 12);

        var cmd = new CreateReservationCommand(evt.Id, 5, "Juan", "juan@test.com");
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReservation_ForCancelledEvent_ThrowsDomainException()
    {
        var evt = CreateActiveEvent();
        evt.Status = EventStatus.Cancelado;
        _db.SaveChanges();

        var cmd = new CreateReservationCommand(evt.Id, 1, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateReservation_PriceExactly100_HasNoTicketLimit()
    {
        // RN-05 triggers only when price > 100, NOT when price == 100
        var evt = CreateActiveEvent(capacity: 50, price: 100m, hoursFromNow: 72);
        var cmd = new CreateReservationCommand(evt.Id, 11, "Juan", "juan@test.com");
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReservation_ExactlyOneHourFromStart_Succeeds()
    {
        // RN-04 blocks when hoursUntilStart < 1; exactly 1 hour is allowed
        // (1.0 is not < 1)
        var evt = CreateActiveEvent(hoursFromNow: 1.05); // slightly over 1h to be safe
        var cmd = new CreateReservationCommand(evt.Id, 1, "Juan", "juan@test.com");
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReservation_LostCancelledReservations_BlockCapacity()
    {
        // Lost tickets reduce available capacity (they're subtracted in Event.AvailableTickets)
        var evt = CreateActiveEvent(capacity: 5);
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id, Quantity = 5,
            BuyerName = "A", BuyerEmail = "a@test.com",
            Status = ReservationStatus.Cancelada, IsLost = true
        });
        _db.SaveChanges();

        // All 5 seats are lost → 0 available
        var cmd = new CreateReservationCommand(evt.Id, 1, "Pedro", "pedro@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*available*");
    }

    [Fact]
    public async Task CreateReservation_CleanCancelledReservations_DoNotBlockCapacity()
    {
        // Cancelled-without-penalty reservations free up capacity
        var evt = CreateActiveEvent(capacity: 5);
        _db.Reservations.Add(new Reservation
        {
            EventId = evt.Id, Quantity = 5,
            BuyerName = "A", BuyerEmail = "a@test.com",
            Status = ReservationStatus.Cancelada, IsLost = false
        });
        _db.SaveChanges();

        // IsLost = false → seats are free again
        var cmd = new CreateReservationCommand(evt.Id, 5, "Pedro", "pedro@test.com");
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateReservation_PriceJustOver100_CapIs10()
    {
        // 100.01 > 100 → RN-05 applies
        var evt = CreateActiveEvent(capacity: 100, price: 100.01m, hoursFromNow: 72);
        var cmd = new CreateReservationCommand(evt.Id, 11, "Juan", "juan@test.com");
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*10*");
    }

    public void Dispose() => _db.Dispose();
}
