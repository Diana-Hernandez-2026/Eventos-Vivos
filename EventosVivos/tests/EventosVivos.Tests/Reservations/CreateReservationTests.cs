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

    public void Dispose() => _db.Dispose();
}
