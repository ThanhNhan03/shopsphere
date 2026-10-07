using System.Reflection;
using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ShopSphere.SharedKernel;

namespace ShopSphere.Messaging;

public static class ServiceSetup
{
    public static void AddDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSerilog((services, log) => log
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext().Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console()
            .WriteTo.Seq(builder.Configuration["Seq:Url"] ?? "http://localhost:5341"));
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks();
    }

    public static void UseDefaults(this WebApplication app)
    {
        app.UseExceptionHandler(handler => handler.Run(async ctx =>
        {
            var error = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
            var status = error is ApiException api ? api.StatusCode : 500;
            await Results.Problem(statusCode: status,
                detail: status == 500 ? "The service could not complete the request." : error?.Message)
                .ExecuteAsync(ctx);
        }));
        app.UseSerilogRequestLogging();
        app.MapHealthChecks("/health");
    }

    public static void AddServiceBus<T>(this IServiceCollection services, IConfiguration config,
        string service, Assembly consumers) where T : DbContext
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumers(consumers);
            x.AddEntityFrameworkOutbox<T>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.DuplicateDetectionWindow = TimeSpan.FromDays(7);
            });
            x.AddConfigureEndpointsCallback((context, name, cfg) =>
            {
                cfg.UseMessageRetry(r => r.Intervals(200, 1000, 3000));
                cfg.UseEntityFrameworkOutbox<T>(context);
            });
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(config["RabbitMq:Host"] ?? "localhost", "/", h =>
                {
                    h.Username(config["RabbitMq:Username"] ?? "shopsphere");
                    h.Password(config["RabbitMq:Password"] ?? "shopsphere-local");
                });
                cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(service, false));
            });
        });
    }

    public static void AddOutbox(this ModelBuilder model)
    {
        model.AddInboxStateEntity();
        model.AddOutboxMessageEntity();
        model.AddOutboxStateEntity();
    }

    public static async Task MigrateDatabase<T>(this WebApplication app) where T : DbContext
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<T>().Database.MigrateAsync();
    }
}
