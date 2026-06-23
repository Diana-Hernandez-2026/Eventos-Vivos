namespace EventosVivos.Application.Common;

/// <summary>
/// Converts UTC datetimes to the business's local timezone so domain rules
/// can evaluate wall-clock hours and day-of-week correctly.
/// </summary>
public interface IBusinessClock
{
    DateTime ToBusinessLocal(DateTime utcDateTime);
}
