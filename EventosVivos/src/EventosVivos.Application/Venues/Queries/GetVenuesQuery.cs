using EventosVivos.Domain.Interfaces;
using MediatR;

namespace EventosVivos.Application.Venues.Queries;

public record GetVenuesQuery : IRequest<IReadOnlyList<VenueDto>>;

public record VenueDto(int Id, string Name, int Capacity, string City);

public class GetVenuesQueryHandler(IVenueRepository venueRepo) : IRequestHandler<GetVenuesQuery, IReadOnlyList<VenueDto>>
{
    public async Task<IReadOnlyList<VenueDto>> Handle(GetVenuesQuery query, CancellationToken ct)
    {
        var venues = await venueRepo.GetAllAsync(ct);
        return venues.Select(v => new VenueDto(v.Id, v.Name, v.Capacity, v.City)).ToList();
    }
}
