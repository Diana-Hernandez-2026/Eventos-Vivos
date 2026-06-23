using EventosVivos.Domain.Exceptions;
using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Events.Queries.GetOccupancyReport;

public class GetOccupancyReportQueryHandler(IEventRepository eventRepo)
    : IRequestHandler<GetOccupancyReportQuery, OccupancyReportDto>
{
    public async Task<OccupancyReportDto> Handle(GetOccupancyReportQuery query, CancellationToken ct)
    {
        await eventRepo.UpdateCompletedStatusAsync(ct);

        var evt = await eventRepo.GetByIdWithReservationsAsync(query.EventId, ct)
            ?? throw new NotFoundException("Event", query.EventId);

        var confirmed = evt.ConfirmedTickets;
        var lost = evt.LostTickets;
        var available = evt.MaxCapacity - confirmed - lost;
        var occupancyPct = evt.MaxCapacity > 0
            ? Math.Round((decimal)confirmed / evt.MaxCapacity * 100, 2)
            : 0;
        var revenue = confirmed * evt.TicketPrice;

        return new OccupancyReportDto(
            evt.Id, evt.Title, evt.MaxCapacity,
            confirmed, available, lost,
            occupancyPct, revenue, evt.Status.ToString()
        );
    }
}
