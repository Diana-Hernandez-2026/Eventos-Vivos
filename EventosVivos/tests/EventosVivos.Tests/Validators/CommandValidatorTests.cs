using EventosVivos.Application.Events.Commands.CreateEvent;
using EventosVivos.Application.Reservations.Commands.CreateReservation;
using EventosVivos.Domain.Enums;
using FluentAssertions;

namespace EventosVivos.Tests.Validators;

public class CreateEventCommandValidatorTests
{
    private readonly CreateEventCommandValidator _validator = new();

    private static CreateEventCommand ValidCommand() => new(
        Title: "Conferencia Anual",
        Description: "Una descripción válida para el evento",
        VenueId: 1,
        MaxCapacity: 100,
        StartDateTime: DateTime.UtcNow.AddDays(10),
        EndDateTime: DateTime.UtcNow.AddDays(10).AddHours(4),
        TicketPrice: 50m,
        Type: EventType.Conferencia);

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("ab")]
    public void Title_TooShortOrEmpty_FailsValidation(string title)
    {
        var result = _validator.Validate(ValidCommand() with { Title = title });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.Title));
    }

    [Fact]
    public void Title_ExactlyFiveChars_PassesValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Title = "Cinco" });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(CreateEventCommand.Title));
    }

    [Fact]
    public void Description_TooShort_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Description = "Corto" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.Description));
    }

    [Fact]
    public void Description_ExactlyTenChars_PassesValidation()
    {
        var result = _validator.Validate(ValidCommand() with { Description = "1234567890" });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(CreateEventCommand.Description));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void TicketPrice_ZeroOrNegative_FailsValidation(decimal price)
    {
        var result = _validator.Validate(ValidCommand() with { TicketPrice = price });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.TicketPrice));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void MaxCapacity_ZeroOrNegative_FailsValidation(int capacity)
    {
        var result = _validator.Validate(ValidCommand() with { MaxCapacity = capacity });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.MaxCapacity));
    }

    [Fact]
    public void StartDateTime_InPast_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { StartDateTime = DateTime.UtcNow.AddHours(-1) });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.StartDateTime));
    }

    [Fact]
    public void EndDateTime_BeforeStartDateTime_FailsValidation()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var result = _validator.Validate(ValidCommand() with
        {
            StartDateTime = start,
            EndDateTime   = start.AddHours(-1)
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.EndDateTime));
    }

    [Fact]
    public void EndDateTime_EqualToStartDateTime_FailsValidation()
    {
        var start = DateTime.UtcNow.AddDays(5);
        var result = _validator.Validate(ValidCommand() with
        {
            StartDateTime = start,
            EndDateTime   = start
        });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.EndDateTime));
    }

    [Fact]
    public void VenueId_Zero_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { VenueId = 0 });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateEventCommand.VenueId));
    }

    [Fact]
    public void MultipleViolations_ReportsAllErrors()
    {
        var result = _validator.Validate(ValidCommand() with
        {
            Title       = "",
            Description = "",
            TicketPrice = -1,
            MaxCapacity = 0
        });
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
    }
}

public class CreateReservationCommandValidatorTests
{
    private readonly CreateReservationCommandValidator _validator = new();

    private static CreateReservationCommand ValidCommand() => new(
        EventId:    Guid.NewGuid(),
        Quantity:   2,
        BuyerName:  "Juan Pérez",
        BuyerEmail: "juan@example.com");

    [Fact]
    public void ValidCommand_PassesValidation()
    {
        _validator.Validate(ValidCommand()).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("nodot")]
    public void BuyerEmail_Invalid_FailsValidation(string email)
    {
        var result = _validator.Validate(ValidCommand() with { BuyerEmail = email });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateReservationCommand.BuyerEmail));
    }

    [Fact]
    public void BuyerEmail_Valid_PassesValidation()
    {
        var result = _validator.Validate(ValidCommand() with { BuyerEmail = "usuario@dominio.co" });
        result.Errors.Should().NotContain(e => e.PropertyName == nameof(CreateReservationCommand.BuyerEmail));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Quantity_ZeroOrNegative_FailsValidation(int quantity)
    {
        var result = _validator.Validate(ValidCommand() with { Quantity = quantity });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateReservationCommand.Quantity));
    }

    [Fact]
    public void Quantity_One_PassesValidation()
    {
        _validator.Validate(ValidCommand() with { Quantity = 1 }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void BuyerName_Empty_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { BuyerName = "" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateReservationCommand.BuyerName));
    }

    [Fact]
    public void EventId_Empty_FailsValidation()
    {
        var result = _validator.Validate(ValidCommand() with { EventId = Guid.Empty });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateReservationCommand.EventId));
    }
}
