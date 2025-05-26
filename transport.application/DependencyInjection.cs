using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Transport.Domain.Drivers.Abstraction;
using Transport.Domain.Users.Abstraction;
using Transport.Domain.Vehicles.Abstraction;
using Transport.Domain.Cities.Abstraction;
using Transport.Domain.Services.Abstraction;
using Transport.Domain.Customers.Abstraction;
using Transport.Domain.Reserves.Abstraction;
using Transport.Domain.Directions.Abstraction;

namespace Transport.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IUserBusiness, UserBusiness.UserBusiness>();
        services.AddScoped<IDriverBusiness, DriverBusiness.DriverBusiness>();
        services.AddScoped<IVehicleBusiness, VehicleBusiness.VehicleBusiness>();
        services.AddScoped<ICityBusiness, CityBusiness.CityBusiness>();
        services.AddScoped<IVehicleTypeBusiness, VehicleTypeBusiness.VehicleTypeBusiness>();
        services.AddScoped<IServiceBusiness, ServiceBusiness.ServiceBusiness>();
        services.AddScoped<ICustomerBusiness, CustomerBusiness.CustomerBusiness>();
        services.AddScoped<IReserveBusiness, ReserveBusiness.ReserveBusiness>();
        services.AddScoped<IDirectionBusiness, DirectionBusiness.DirectionBusiness>();

        return services;
    }
}

