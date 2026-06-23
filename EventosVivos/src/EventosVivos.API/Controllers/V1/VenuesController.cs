using Asp.Versioning;
using EventosVivos.Application.Venues.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using EventosVivos.API.Configuration;

namespace EventosVivos.API.Controllers.V1;

/// <summary>Read-only reference data for event venues.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/venues")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Api)]
[Produces("application/json")]
public class VenuesController(IMediator mediator) : ControllerBase
{
    /// <summary>Returns all available venues (no authentication required).</summary>
    /// <response code="200">List of venues with id, name, capacity and city.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VenueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVenues(CancellationToken ct)
    {
        var result = await mediator.Send(new GetVenuesQuery(), ct);
        return Ok(result);
    }
}
