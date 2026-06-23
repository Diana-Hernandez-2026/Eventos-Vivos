using EventosVivos.Application.Reservations.Commands.CancelReservation;
using EventosVivos.Application.Reservations.Commands.ConfirmPayment;
using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Repositories;
using EventosVivos.Tests.Helpers;
using FluentAssertions;

namespace EventosVivos.Tests.Reservations;

public class ConfirmAndCancelReservationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ConfirmPaymentCommandHandler _confirmHandler;
    private readonly CancelReservationCommandHandler _cancelHandler;

    public ConfirmAndCancelReservationTests()
    {
        _db = TestDbContextFactory.Create();
        var reservationRepo = new ReservationRepository(_db);
        var eventRepo = new EventRepository(_db);
        _confirmHandler = new ConfirmPaymentCommandHandler(reservationRepo);
        _cancelHandler = new CancelReservationCommandHandler(reservationRepo, eventRepo);
    }

    private (Event evt, Reservation reservation) CreateEventAndReservation(
        ReservationStatus status = ReservationStatus.PendientePago,
        double eventHoursFromNow = 72)
    {
        var start = DateTime.UtcNow.AddHours(eventHoursFromNow);
        var evt = new Event
        {
            Title = "Test Event",
            Description = "Test description",
            VenueId = 1,
            MaxCapacity = 100,
            StartDateTime = start,
            EndDateTime = start.AddHours(4),
            TicketPrice = 50m,
            Type = EventType.Conferencia,
            Status = EventStatus.Activo
        };
        _db.Events.Add(evt);

        var reservation = new Reservation
        {
            EventId = evt.Id,
            Quantity = 2,
            BuyerName = "Ana García",
            BuyerEmail = "ana@test.com",
            Status = status
        };
        _db.Reservations.Add(reservation);
        _db.SaveChanges();

        return (evt, reservation);
    }

    [Fact]
    public async Task ConfirmPayment_WithPendingReservation_Succeeds()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.PendientePago);

        var result = await _confirmHandler.Handle(new ConfirmPaymentCommand(reservation.Id), CancellationToken.None);

        result.Status.Should().Be("Confirmada");
        result.ReservationCode.Should().MatchRegex(@"^EV-\d{6}$");
    }

    [Fact]
    public async Task ConfirmPayment_AlreadyConfirmed_ThrowsConflictException()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada);

        var act = () => _confirmHandler.Handle(new ConfirmPaymentCommand(reservation.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ConfirmPayment_CancelledReservation_ThrowsDomainException()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Cancelada);

        var act = () => _confirmHandler.Handle(new ConfirmPaymentCommand(reservation.Id), CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CancelReservation_WithConfirmedReservation_Succeeds()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada, eventHoursFromNow: 72);

        var result = await _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.Status.Should().Be("Cancelada");
        result.IsLost.Should().BeFalse(); // 72h > 48h → no penalty
        result.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelReservation_Within48hOfEvent_MarksAsLost()
    {
        // Event starts in 24 hours (within 48h window → penalty applies)
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada, eventHoursFromNow: 24);

        var result = await _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsLost.Should().BeTrue();
    }

    [Fact]
    public async Task CancelReservation_AlreadyCancelled_ThrowsConflictException()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Cancelada);

        var act = () => _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CancelReservation_PendingPayment_ThrowsDomainException()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.PendientePago);

        var act = () => _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*pendiente_pago*");
    }

    [Fact]
    public async Task CancelReservation_WhenNotConfirmed_ShouldThrow()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.PendientePago);

        var act = () => _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CancelReservation_SafelyAbove48Hours_ShouldNotBeLost()
    {
        // 48.5h is safely above the 48h threshold even accounting for test execution time
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada, eventHoursFromNow: 48.5);

        var result = await _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);
        result.IsLost.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmPayment_GeneratesUniqueCode()
    {
        var (_, r1) = CreateEventAndReservation(ReservationStatus.PendientePago);
        var (_, r2) = CreateEventAndReservation(ReservationStatus.PendientePago);

        var res1 = await _confirmHandler.Handle(new ConfirmPaymentCommand(r1.Id), CancellationToken.None);
        var res2 = await _confirmHandler.Handle(new ConfirmPaymentCommand(r2.Id), CancellationToken.None);

        res1.ReservationCode.Should().NotBe(res2.ReservationCode);
    }

    [Fact]
    public async Task ConfirmPayment_ShouldNotRegenerateCodeIfAlreadyConfirmed()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.PendientePago);

        var first = await _confirmHandler.Handle(new ConfirmPaymentCommand(reservation.Id), CancellationToken.None);
        var act = () => _confirmHandler.Handle(new ConfirmPaymentCommand(reservation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();

        first.ReservationCode.Should().NotBeNull();
    }

    [Fact]
    public async Task ConfirmPayment_EventNotFound_ShouldThrow()
    {
        var act = () => _confirmHandler.Handle(new ConfirmPaymentCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CancelReservation_EventNotFound_ShouldThrow()
    {
        var act = () => _cancelHandler.Handle(new CancelReservationCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CancelReservation_47HoursFromEvent_AppliesPenalty()
    {
        // 47 hours < 48 hours threshold → penalty must apply
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada, eventHoursFromNow: 47);

        var result = await _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsLost.Should().BeTrue();
        result.Status.Should().Be("Cancelada");
    }

    [Fact]
    public async Task CancelReservation_49HoursFromEvent_NoPenalty()
    {
        // 49 hours > 48 hours threshold → no penalty
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada, eventHoursFromNow: 49);

        var result = await _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.IsLost.Should().BeFalse();
    }

    [Fact]
    public async Task CancelReservation_SetsTimestamp()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada, eventHoursFromNow: 72);

        var result = await _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ConfirmPayment_ReturnsCorrectReservationId()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.PendientePago);

        var result = await _confirmHandler.Handle(new ConfirmPaymentCommand(reservation.Id), CancellationToken.None);

        result.ReservationId.Should().Be(reservation.Id);
    }

    [Fact]
    public async Task CancelReservation_ReturnsCorrectReservationId()
    {
        var (_, reservation) = CreateEventAndReservation(ReservationStatus.Confirmada);

        var result = await _cancelHandler.Handle(new CancelReservationCommand(reservation.Id), CancellationToken.None);

        result.ReservationId.Should().Be(reservation.Id);
    }

    public void Dispose() => _db.Dispose();
}
