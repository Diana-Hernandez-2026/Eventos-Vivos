namespace EventosVivos.Domain.Entities;

public class Venue
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string City { get; set; } = string.Empty;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
