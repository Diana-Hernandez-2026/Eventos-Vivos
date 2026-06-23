using System.Text;

namespace EventosVivos.Application.Common;

public static class CursorEncoder
{
    public static string Encode(DateTime createdAt, Guid id) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{createdAt:O}|{id}"));

    public static (DateTime CreatedAt, Guid Id)? Decode(string cursor)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = decoded.Split('|');
            if (parts.Length != 2) return null;
            if (!DateTime.TryParse(parts[0], out var createdAt)) return null;
            if (!Guid.TryParse(parts[1], out var id)) return null;
            return (createdAt.ToUniversalTime(), id);
        }
        catch { return null; }
    }
}
