using Asp.Versioning;
using EventosVivos.Application.Reservations.Commands.CancelReservation;
using EventosVivos.Application.Reservations.Commands.ConfirmPayment;
using EventosVivos.Application.Reservations.Commands.CreateReservation;
using EventosVivos.Application.Reservations.Queries.GetReservation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using EventosVivos.API.Configuration;

namespace EventosVivos.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reservations")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
public class ReservationsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetReservation(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetReservationQuery(id), ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return StatusCode(201, result);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmPayment(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmPaymentCommand(id), ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelReservation(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new CancelReservationCommand(id), ct);
        return Ok(result);
    }
}
