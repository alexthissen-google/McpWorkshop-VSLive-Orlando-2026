using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TravelBooking.Domain.Services;
using TravelBooking.Infrastructure.Data.Seeding;
using TravelBooking.Infrastructure.Services;

namespace TravelBooking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTravelBookingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BookingOptions>(configuration.GetSection(BookingOptions.SectionName));

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IFlightService, FlightService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<DataSeeder>();

        return services;
    }
}
