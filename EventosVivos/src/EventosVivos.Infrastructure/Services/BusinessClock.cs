using EventosVivos.Application.Common;
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
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.Utc;
    }
}
