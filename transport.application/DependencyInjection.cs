using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Transport.Business.UserBusiness;
using Transport.Business.DriverBusiness;

namespace transport.application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<ILoginBusiness, LoginBusiness>();
        services.AddScoped<IDriverBusiness, DriverBusiness>();
        return services;
    }
}

