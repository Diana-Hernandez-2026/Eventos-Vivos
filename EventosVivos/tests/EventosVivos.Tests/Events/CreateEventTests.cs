using EventosVivos.Application.Common;
using EventosVivos.Application.Events.Commands.CreateEvent;
using EventosVivos.Domain.Enums;
using EventosVivos.Domain.Exceptions;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Repositories;
using EventosVivos.Tests.Helpers;
using FluentAssertions;

namespace EventosVivos.Tests.Events;

public class CreateEventTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateEventCommandHandler _handler;

    // Stub that treats UTC as business-local time (no offset), keeping tests timezone-agnostic.
    private sealed class UtcClock : IBusinessClock
    {
        public DateTime ToBusinessLocal(DateTime utcDateTime) => utcDateTime;
    }

    public CreateEventTests()
    {
        _db = TestDbContextFactory.Create();
        _handler = new CreateEventCommandHandler(
            new EventRepository(_db),
            new VenueRepository(_db),
            new UtcClock());
    }

    // ValidCommand defaults to venue 1 (capacity 200). MaxCapacity = 100 is safe for venue 1.
    // Use venue 2 (capacity 50) or venue 3 (capacity 500) only with matching MaxCapacity.
    private static CreateEventCommand ValidCommand(int venueId = 1, int maxCapacity = 100) => new(
        Title: "Test Conference",
        Description: "A test conference event description",
        VenueId: venueId,
        MaxCapacity: maxCapacity,
        StartDateTime: NextWeekday(DateTime.UtcNow.AddDays(5), 10),
        EndDateTime: NextWeekday(DateTime.UtcNow.AddDays(5), 10).AddHours(4),
        TicketPrice: 50m,
        Type: EventType.Conferencia
    );

    // Returns a future DateTime pinned to a weekday at the given hour to avoid weekend/22h edge cases.
    private static DateTime NextWeekday(DateTime from, int hour)
    {
        var d = from.Date.AddHours(hour);
        while (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) d = d.AddDays(1);
        if (d <= DateTime.UtcNow) d = d.AddDays(7);
        return d;
    }

    // Returns a future Saturday with the given hour/minute.
    private static DateTime NextSaturday(int hour, int minute = 0)
    {
        var d = DateTime.UtcNow.AddDays(1).Date;
        while (d.DayOfWeek != DayOfWeek.Saturday) d = d.AddDays(1);
        var result = d.AddHours(hour).AddMinutes(minute);
        return result <= DateTime.UtcNow ? result.AddDays(7) : result;
    }

    // ── RN-03: weekend time restriction ─────────────────────────────────────

    [Fact]
    public async Task CreateEvent_OnSundayAfter22h_ThrowsDomainException()
    {
        var sunday = DateTime.UtcNow.AddDays(1).Date;
        while (sunday.DayOfWeek != DayOfWeek.Sunday) sunday = sunday.AddDays(1);
        var sundayAt23 = new DateTime(sunday.Year, sunday.Month, sunday.Day, 23, 0, 0, DateTimeKind.Utc);
        if (sundayAt23 <= DateTime.UtcNow) sundayAt23 = sundayAt23.AddDays(7);

        var cmd = ValidCommand() with { StartDateTime = sundayAt23, EndDateTime = sundayAt23.AddHours(2) };
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();
    }

    [Fact]
    public async Task CreateEvent_OnWeekendAfter22h_ThrowsDomainException()
    {
        var saturdayAt2230 = NextSaturday(22, 30);
        var cmd = ValidCommand() with { StartDateTime = saturdayAt2230, EndDateTime = saturdayAt2230.AddHours(2) };
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*22:00*");
    }

    [Fact]
    public async Task CreateEvent_OnWeekendBefore22h_Succeeds()
    {
        var saturdayAt19 = NextSaturday(19);
        var cmd = ValidCommand() with { StartDateTime = saturdayAt19, EndDateTime = saturdayAt19.AddHours(2) };
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEvent_AtExactly22h_IsAllowed()
    {
        // RN-03 is "después de las 22:00" — 22:00:00 exactly is the boundary and is permitted
        var saturdayAt22 = NextSaturday(22, 0);
        var cmd = ValidCommand() with { StartDateTime = saturdayAt22, EndDateTime = saturdayAt22.AddHours(2) };
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEvent_WeekdayAfter22h_Succeeds()
    {
        // RN-03 applies to weekends only — a weekday at 23:00 must be allowed
        var weekdayAt23 = NextWeekday(DateTime.UtcNow.AddDays(1), 23);
        var cmd = ValidCommand() with { StartDateTime = weekdayAt23, EndDateTime = weekdayAt23.AddHours(2) };
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    // ── RN-01: capacity cannot exceed venue capacity ─────────────────────────

    [Fact]
    public async Task CreateEvent_ExceedingVenueCapacity_ThrowsDomainException()
    {
        var cmd = ValidCommand(venueId: 2, maxCapacity: 51); // venue 2 capacity = 50
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*capacity*");
    }

    [Fact]
    public async Task CreateEvent_CapacityExactlyEqualsVenueCapacity_Succeeds()
    {
        var cmd = ValidCommand(venueId: 2, maxCapacity: 50); // exactly at venue 2 limit
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEvent_Venue3_CanHostLargeEvent()
    {
        var cmd = ValidCommand(venueId: 3, maxCapacity: 500); // venue 3 (Arena Sur) capacity = 500
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    // ── RN-02: no overlapping events at the same venue ──────────────────────

    [Fact]
    public async Task CreateEvent_WithOverlappingVenueSchedule_ThrowsDomainException()
    {
        var start = NextWeekday(DateTime.UtcNow.AddDays(5), 10);
        await _handler.Handle(ValidCommand() with { StartDateTime = start, EndDateTime = start.AddHours(4) }, CancellationToken.None);

        var cmd2 = ValidCommand() with { StartDateTime = start.AddHours(2), EndDateTime = start.AddHours(6) };
        var act = () => _handler.Handle(cmd2, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>().WithMessage("*overlap*");
    }

    [Fact]
    public async Task CreateEvent_StartingExactlyWhenAnotherEnds_ShouldSucceed()
    {
        // Adjacent events (no gap, no overlap) at the same venue must be allowed
        var start = NextWeekday(DateTime.UtcNow.AddDays(5), 10);
        await _handler.Handle(ValidCommand() with { StartDateTime = start, EndDateTime = start.AddHours(2) }, CancellationToken.None);

        var cmd2 = ValidCommand() with { StartDateTime = start.AddHours(2), EndDateTime = start.AddHours(4) };
        var result = await _handler.Handle(cmd2, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateEvent_SameTimeDifferentVenue_ShouldSucceed()
    {
        // Same time slot, different venues — no overlap constraint between venues
        var start = NextWeekday(DateTime.UtcNow.AddDays(5), 10);
        await _handler.Handle(
            ValidCommand(venueId: 1) with { StartDateTime = start, EndDateTime = start.AddHours(2) },
            CancellationToken.None);

        // Venue 3 (capacity 500) avoids the capacity check conflict
        var result = await _handler.Handle(
            ValidCommand(venueId: 3, maxCapacity: 200) with { StartDateTime = start, EndDateTime = start.AddHours(2) },
            CancellationToken.None);
        result.Should().NotBeNull();
    }

    // ── General creation ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEvent_WithValidData_Succeeds()
    {
        var result = await _handler.Handle(ValidCommand(), CancellationToken.None);
        result.Should().NotBeNull();
        result.Title.Should().Be("Test Conference");
        result.Status.Should().Be(EventStatus.Activo);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateEvent_WithNonExistentVenue_ThrowsNotFoundException()
    {
        var cmd = ValidCommand(venueId: 999);
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    public void Dispose() => _db.Dispose();
}
