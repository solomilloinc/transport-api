using Azure.Messaging.ServiceBus;
using FluentEmail.Core;
using FluentEmail.Smtp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Transport.Business.Authentication;
using Transport.Business.Authorization;
using Transport.Business.Data;
using Transport.Business.Messaging;
using Transport.Business.Services.Email;
using Transport.Business.Services.Payment;
using Transport.Infraestructure.Authentication;
using Transport.Infraestructure.Authorization;
using Transport.Infraestructure.Database;
using Transport.Infraestructure.Messaging;
using Transport.Infraestructure.Services.Email;
using Transport.Infraestructure.Services.Payment;
using Transport.Infraestructure.Time;
using Transport.SharedKernel;

namespace Transport.Infraestructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddServices(configuration)
            .AddDatabase(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal()
            .AddMessaging(configuration);

    private static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IMercadoPagoPaymentGateway, MercadoPagoPaymentGateway>();

        //var smtpSection = configuration.GetSection("SmtpSettingOption");
        //var smtpHost = smtpSection.GetValue<string>("Host");
        //var smtpPort = smtpSection.GetValue<int>("Port");
        //var smtpUser = smtpSection.GetValue<string>("User");
        //var smtpPass = smtpSection.GetValue<string>("Password");
        //var smtpFromEmail = smtpSection.GetValue<string>("FromEmail");
        //var smtpFromName = smtpSection.GetValue<string>("FromName");

        //var smtpClient = new System.Net.Mail.SmtpClient(smtpHost)
        //{
        //    Port = smtpPort,
        //    Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass),
        //    EnableSsl = true,
        //};

        //var sender = new SmtpSender(() => smtpClient);

        //Email.DefaultSender = sender;

        //services
        //    .AddFluentEmail(smtpFromEmail, smtpFromName)
        //    .AddSmtpSender(smtpClient);

        //services.AddScoped<IEmailSender, EmailSender>();

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName)
                          .EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null))
                          .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddSqlServer(configuration.GetConnectionString("Database")!);

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();
        services.AddScoped<IUserContext, UserContext>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddScoped<Transport.Business.Authorization.IPermissionService, Transport.Infraestructure.Authorization.PermissionProvider>();
        services.AddScoped<Authorization.IAuthorizationService, AuthorizationService>();
        services.AddScoped<IJwtService, JwtService>();

        return services;
    }

    private static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceBusConnectionString = configuration.GetValue<string>("ServiceBusConnection");

        services.AddSingleton<ServiceBusClient>(provider =>
        {
            return new ServiceBusClient(serviceBusConnectionString);
        });

        services.AddSingleton<IOutboxDispatcher, OutboxDispatcher>();

        return services;
    }
}
