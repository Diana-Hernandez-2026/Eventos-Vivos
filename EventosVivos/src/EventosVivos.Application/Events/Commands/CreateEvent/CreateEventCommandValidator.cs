using EventosVivos.Application.Common;
using FluentValidation;

namespace EventosVivos.Application.Events.Commands.CreateEvent;

public class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 100);
        RuleFor(x => x.Description).NotEmpty().Length(10, 500);
        RuleFor(x => x.VenueId).GreaterThan(0);
        RuleFor(x => x.MaxCapacity).GreaterThan(0);
        RuleFor(x => x.TicketPrice).GreaterThan(0);

        RuleFor(x => x.StartDateTime)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage(_ => I18n.StartDateFuture);

        RuleFor(x => x.EndDateTime)
            .GreaterThan(x => x.StartDateTime)
            .WithMessage(_ => I18n.EndDateAfterStart);
    }
}
