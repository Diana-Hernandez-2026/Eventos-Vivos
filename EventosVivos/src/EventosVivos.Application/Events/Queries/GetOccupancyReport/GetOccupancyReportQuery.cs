using MediatR;

namespace EventosVivos.Application.Events.Queries.GetOccupancyReport;

public record GetOccupancyReportQuery(Guid EventId) : IRequest<OccupancyReportDto>;

public record OccupancyReportDto(
    Guid EventId,
    string Title,
    int MaxCapacity,
    int ConfirmedTickets,
    int AvailableTickets,
    int LostTickets,
    decimal OccupancyPercentage,
    decimal TotalRevenue,
    string Status
);
