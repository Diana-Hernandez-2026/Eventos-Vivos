using Asp.Versioning;
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

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/events")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Api)]
public class EventsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
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

    [HttpPost]
    public async Task<IActionResult> CreateEvent([FromBody] CreateEventCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetEventReport), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}/report")]
    [AllowAnonymous]
    public async Task<IActionResult> GetEventReport(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOccupancyReportQuery(id), ct);
        return Ok(result);
    }
}
