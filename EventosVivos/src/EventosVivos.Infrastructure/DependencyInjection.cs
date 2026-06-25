using EventosVivos.Application.Common;
using EventosVivos.Domain.Interfaces;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Repositories;
using EventosVivos.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventosVivos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured. " +
                "Set ConnectionStrings__DefaultConnection as an Application Setting in Azure App Service.");

        if (connectionString.Contains("Active Directory", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The connection string uses Azure AD authentication ('Authentication=Active Directory Default'), " +
                "which requires Managed Identity. Use SQL authentication instead: " +
                "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;" +
                "User Id=<user>;Password=<pass>;Encrypt=True;TrustServerCertificate=False;");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (environmentName != "Staging" && environmentName != "Production")
                options.UseSqlite(connectionString);
            else
                options.UseSqlServer(connectionString, sql =>
                    sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null));
        });

        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();

        services.AddSingleton<IBusinessClock, BusinessClock>();

        return services;
    }
}
