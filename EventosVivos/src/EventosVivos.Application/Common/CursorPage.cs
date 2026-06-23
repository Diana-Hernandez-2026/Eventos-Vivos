namespace EventosVivos.Application.Common;

public record CursorPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasNextPage,
    int Count
);
