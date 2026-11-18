using Microsoft.Extensions.DependencyInjection;
using TravelBooking.Application.Handlers;

namespace TravelBooking.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTravelBookingApplication(this IServiceCollection services)
    {
        services.AddScoped<SearchFlightsHandler>();
        services.AddScoped<GetFlightDetailsHandler>();
        services.AddScoped<HoldSeatHandler>();
        services.AddScoped<ConfirmBookingHandler>();
        services.AddScoped<GetBookingHandler>();
        services.AddScoped<CancelBookingHandler>();
        return services;
    }
}
