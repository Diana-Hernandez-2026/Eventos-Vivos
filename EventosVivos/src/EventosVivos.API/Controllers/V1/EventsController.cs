using Asp.Versioning;
using EventosVivos.Application.Common;
using EventosVivos.Application.Events.Commands.CreateEvent;
using EventosVivos.Application.Events.Queries.GetEvents;
using EventosVivos.Application.Events.Queries.GetOccupancyReport;
using EventosVivos.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using EventosVivos.API.Configuration;

namespace EventosVivos.API.Controllers.V1;

/// <summary>Manage events: listing, creation and occupancy reporting.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
[Produces("application/json")]
public class EventsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Returns a cursor-paginated list of events with optional filters.
    /// </summary>
    /// <remarks>
    /// Pass the <c>nextCursor</c> value from a previous response as <c>cursor</c> to fetch
    /// the next page. All filter parameters are optional and combinable.
    ///
    /// Business rules evaluated on each call:
    /// - Events whose <c>endDateTime</c> is in the past are automatically marked as <c>Completado</c>.
    /// </remarks>
    /// <param name="type">Filter by event type (Conferencia, Taller, Concierto).</param>
    /// <param name="startFrom">Only events starting on or after this date (UTC).</param>
    /// <param name="startTo">Only events starting on or before this date (UTC).</param>
    /// <param name="venueId">Filter by venue ID.</param>
    /// <param name="status">Filter by status (Activo, Cancelado, Completado).</param>
    /// <param name="titleSearch">Case-insensitive substring match on title.</param>
    /// <param name="cursor">Opaque pagination cursor from a previous response.</param>
    /// <param name="limit">Page size, 1–100 (default 20).</param>
    /// <response code="200">Paginated list of events.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CursorPage<EventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] EventType? type,
        [FromQuery] DateTime? startFrom,
        [FromQuery] DateTime? startTo,
        [FromQuery] int? venueId,
        [FromQuery] EventStatus? status,
        [FromQuery] string? titleSearch,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetEventsQuery(type, startFrom, startTo, venueId, status, titleSearch, cursor, limit), ct);
        return Ok(result);
    }

    /// <summary>Creates a new event (authentication required).</summary>
    /// <remarks>
    /// Business rules enforced:
    /// - **RN-01** MaxCapacity must not exceed the venue's physical capacity.
    /// - **RN-02** No time-slot overlap with another active event at the same venue.
    /// - **RN-03** Weekend events (Sat/Sun) cannot start at or after 22:00 (business local time).
    /// </remarks>
    /// <response code="201">Event created. Location header points to the occupancy report.</response>
    /// <response code="401">Missing or invalid JWT.</response>
    /// <response code="404">Venue not found.</response>
    /// <response code="422">Validation error or business rule violation.</response>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(CreateEventResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetEventReport), new { id = result.Id }, result);
    }

    /// <summary>Returns the occupancy and revenue report for an event (no authentication required).</summary>
    /// <param name="id">Event GUID.</param>
    /// <response code="200">Occupancy report including confirmed tickets, available seats, lost tickets, and total revenue.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:guid}/report")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(OccupancyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEventReport(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOccupancyReportQuery(id), ct);
        return Ok(result);
    }
}
