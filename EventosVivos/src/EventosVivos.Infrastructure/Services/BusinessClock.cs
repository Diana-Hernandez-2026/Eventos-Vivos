using EventosVivos.Application.Common;
using EventosVivos.API.Configuration;
using Microsoft.Extensions.Options;

namespace EventosVivos.Infrastructure.Services;

public class BusinessClock(IOptions<BusinessSettings> settings) : IBusinessClock
{
    private readonly TimeZoneInfo _tz = ResolveTimeZone(settings.Value.TimeZoneId);

    public DateTime ToBusinessLocal(DateTime utcDateTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), _tz);

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        // .NET 6+ supports IANA IDs on all platforms; fall back to UTC if the ID is unknown.
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.Utc;
    }
}
