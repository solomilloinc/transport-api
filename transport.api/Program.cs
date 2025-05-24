using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Transport.Infraestructure;
using Transport.Business;
using Microsoft.Extensions.Configuration;
using Transport_Api.Middleware;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Abstractions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Configurations;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Transport.SharedKernel.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

var host = new HostBuilder()
     .ConfigureFunctionsWorkerDefaults(x =>
     {
         x.UseMiddleware<ExceptionHandlingMiddleware>();
         x.UseMiddleware<AuthorizationMiddleware>();
     })
    .ConfigureOpenApi()
    .ConfigureAppConfiguration(c =>
    {
        c.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddApplication()
                .AddInfrastructure(configuration);

        services.AddSingleton<IOpenApiConfigurationOptions>(_ =>
        {
            OpenApiConfigurationOptions options = new OpenApiConfigurationOptions
            {
                Info = new OpenApiInfo
                {
                    Version = "2.0",
                    Title = "Serverless Job Transport API",
                    Description = "This is the API on which the serverless job portal engine is running.",
                    TermsOfService = new Uri("https://www.jobportal.se"),
                    Contact = new OpenApiContact
                    {
                        Name = "Solomillo.inc",
                        Email = "solomilloinc@gmail.com",
                        Url = new Uri("https://www.solomilloinc.se")
                    },
                    License = new OpenApiLicense
                    {
                        Name = "License",
                        Url = new Uri("https://www.solomilloinc.se")
                    }
                },
                OpenApiVersion = OpenApiVersionType.V3,
                IncludeRequestingHostName = true,
                ForceHttps = false,
                ForceHttp = false,
            };
            return options;
        });

        services.AddOptions<JwtOption>().Configure<IConfiguration>((s, c) =>
               c.GetSection(nameof(JwtOption)).Bind(s));
        services.AddSingleton<IJwtOption>(x => x.GetRequiredService<IOptions<JwtOption>>().Value);

        services.AddOptions<ReserveOption>().Configure<IConfiguration>((s, c) =>
              c.GetSection(nameof(ReserveOption)).Bind(s));
        services.AddSingleton<IReserveOption>(x => x.GetRequiredService<IOptions<ReserveOption>>().Value);
    })
    .Build();

host.Run();
