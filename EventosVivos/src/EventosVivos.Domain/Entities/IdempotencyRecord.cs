namespace EventosVivos.Domain.Entities;

public class IdempotencyRecord
{
    public Guid Id { get; set; }
    public string RequestPath { get; set; } = string.Empty;
    public string RequestBodyHash { get; set; } = string.Empty;
    public int ResponseStatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public string ResponseContentType { get; set; } = "application/json";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
