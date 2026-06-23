using Asp.Versioning;
using EventosVivos.Application.Venues.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using EventosVivos.API.Configuration;

namespace EventosVivos.API.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/venues")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Api)]
public class VenuesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetVenues(CancellationToken ct)
    {
        var result = await mediator.Send(new GetVenuesQuery(), ct);
        return Ok(result);
    }
}
