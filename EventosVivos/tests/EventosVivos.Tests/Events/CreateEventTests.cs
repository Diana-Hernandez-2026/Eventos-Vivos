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

    private static CreateEventCommand ValidCommand(int venueId = 1) => new(
        Title: "Test Conference",
        Description: "A test conference event description",
        VenueId: venueId,
        MaxCapacity: 100,
        StartDateTime: DateTime.UtcNow.AddDays(10),
        EndDateTime: DateTime.UtcNow.AddDays(10).AddHours(4),
        TicketPrice: 50m,
        Type: EventType.Conferencia
    );

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

    [Fact]
    public async Task CreateEvent_ExceedingVenueCapacity_ThrowsDomainException()
    {
        // Venue 2 has capacity 50
        var cmd = ValidCommand(venueId: 2) with { MaxCapacity = 100 };
        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*capacity*");
    }

    [Fact]
    public async Task CreateEvent_WithOverlappingVenueSchedule_ThrowsDomainException()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var cmd1 = ValidCommand() with
        {
            StartDateTime = start,
            EndDateTime = start.AddHours(4)
        };
        await _handler.Handle(cmd1, CancellationToken.None);

        // Overlapping event at same venue
        var cmd2 = ValidCommand() with
        {
            StartDateTime = start.AddHours(2),
            EndDateTime = start.AddHours(6)
        };
        var act = () => _handler.Handle(cmd2, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*overlap*");
    }

    [Fact]
    public async Task CreateEvent_OnWeekendAfter22h_ThrowsDomainException()
    {
        // Find next Saturday
        var now = DateTime.UtcNow;
        var saturday = now.AddDays(1);
        while (saturday.DayOfWeek != DayOfWeek.Saturday) saturday = saturday.AddDays(1);
        // Set time to 22:30 (Saturday night)
        var saturdayAt2230 = new DateTime(saturday.Year, saturday.Month, saturday.Day, 22, 30, 0, DateTimeKind.Utc);

        // Only test if this date is in the future
        if (saturdayAt2230 <= DateTime.UtcNow) saturdayAt2230 = saturdayAt2230.AddDays(7);

        var cmd = ValidCommand() with
        {
            StartDateTime = saturdayAt2230,
            EndDateTime = saturdayAt2230.AddHours(2)
        };

        var act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*22:00*");
    }

    [Fact]
    public async Task CreateEvent_OnWeekendBefore22h_Succeeds()
    {
        var saturday = DateTime.UtcNow.AddDays(1);
        while (saturday.DayOfWeek != DayOfWeek.Saturday) saturday = saturday.AddDays(1);
        var saturdayAt19 = new DateTime(saturday.Year, saturday.Month, saturday.Day, 19, 0, 0, DateTimeKind.Utc);
        if (saturdayAt19 <= DateTime.UtcNow) saturdayAt19 = saturdayAt19.AddDays(7);

        var cmd = ValidCommand() with
        {
            StartDateTime = saturdayAt19,
            EndDateTime = saturdayAt19.AddHours(2)
        };

        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    public void Dispose() => _db.Dispose();
}
