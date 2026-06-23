using FluentValidation;

namespace EventosVivos.Application.Reservations.Commands.CreateReservation;

public class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
        RuleFor(x => x.BuyerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BuyerEmail).NotEmpty().EmailAddress();
    }
}
