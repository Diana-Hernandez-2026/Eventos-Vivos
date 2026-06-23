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

/// <summary>Manage reservations: lookup, creation, payment confirmation and cancellation.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reservations")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[Produces("application/json")]
public class ReservationsController(IMediator mediator) : ControllerBase
{
    /// <summary>Retrieves a reservation by its ID.</summary>
    /// <param name="id">Reservation GUID (returned when the reservation was created).</param>
    /// <response code="200">Reservation detail including event info, status, and penalty flag.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">No reservation found with the given ID.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReservationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservation(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetReservationQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Creates a new reservation for an event (authentication required).</summary>
    /// <remarks>
    /// Business rules enforced:
    /// - **RN-04** The event must start more than 1 hour from now.
    /// - **RN-05** If ticket price > $100, maximum 10 tickets per transaction.
    ///
    /// The returned reservation is in `PendientePago` status. Call `POST /{id}/confirm` to pay.
    ///
    /// Include `Idempotency-Key: &lt;UUID&gt;` to make this operation idempotent.
    /// </remarks>
    /// <response code="201">Reservation created with status PendientePago.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="422">Business rule violation (event full, starts too soon, quantity limit, etc.).</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CreateReservationResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return StatusCode(201, result);
    }

    /// <summary>Confirms payment for a reservation in PendientePago status.</summary>
    /// <param name="id">Reservation GUID.</param>
    /// <response code="200">Payment confirmed. Response includes the unique reservation code.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">Reservation not found.</response>
    /// <response code="409">Reservation is not in PendientePago status.</response>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(ConfirmPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmPayment(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new ConfirmPaymentCommand(id), ct);
        return Ok(result);
    }

    /// <summary>Cancels a confirmed reservation.</summary>
    /// <remarks>
    /// Business rule **RN-07**: if the cancellation happens within 48 hours of the event start,
    /// the tickets are marked as **lost** (`isLost: true`) and no refund is issued.
    /// The `isLost` field in the response indicates whether the penalty was applied.
    /// </remarks>
    /// <param name="id">Reservation GUID.</param>
    /// <response code="200">Reservation cancelled. Check <c>isLost</c> for penalty status.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">Reservation not found.</response>
    /// <response code="409">Reservation is already cancelled or in PendientePago status.</response>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(CancelReservationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelReservation(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new CancelReservationCommand(id), ct);
        return Ok(result);
    }
}
