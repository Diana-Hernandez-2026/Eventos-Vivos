namespace EventosVivos.API.Configuration;

public class BusinessSettings
{
    /// <summary>IANA or Windows timezone ID for business rule evaluation (e.g. "America/Bogota").</summary>
    public string TimeZoneId { get; set; } = "America/Bogota";
}
