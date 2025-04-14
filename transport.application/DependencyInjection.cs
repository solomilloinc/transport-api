using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Transport.Business.UserBusiness;
using Transport.Domain.Drivers.Abstraction;
using Transport.Domain.Users.Abstraction;

namespace Transport.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<ILoginBusiness, LoginBusiness>();
        services.AddScoped<IDriverBusiness, DriverBusiness.DriverBusiness>();
        return services;
    }
}

