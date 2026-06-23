using EventosVivos.Domain.Interfaces;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventosVivos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();

        return services;
    }
}
