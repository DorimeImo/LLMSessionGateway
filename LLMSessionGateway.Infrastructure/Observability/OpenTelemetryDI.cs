using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Observability.Shared.Contracts;
using Observability.Shared.DefaultImplementations;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public static class OpenTelemetryDI
    {
        public static IServiceCollection AddOpenTelemetryToAzureMonitorTracing(this IServiceCollection services, IConfiguration config)
        {
            var appInsightsConfigs = config.GetSection("OpenTelemetry:ApplicationInsights").Get<AzureAppInsightsConfigs>();

            services.AddOpenTelemetry()
                 .ConfigureResource(rb =>
                     rb.AddService(serviceName: "LLMSessionGateway"))
                 .WithTracing(builder =>
                 {
                     builder
                         .AddAspNetCoreInstrumentation()   // incoming
                         .AddHttpClientInstrumentation()   // outgoing HTTP
                         .AddGrpcClientInstrumentation()   // outgoing gRPC
                         .AddSource("LLMSessionGateway");  // your custom ActivitySource

                     builder.AddAzureMonitorTraceExporter(o =>
                     {
                         o.ConnectionString = Environment.GetEnvironmentVariable(appInsightsConfigs!.ConnectionString);
                     });
                 });

            services.AddScoped<ITracingService>(sp =>
            {
                var svc = ActivatorUtilities.CreateInstance<OpenTelemetryTracingService>(sp);
                // run after Activity is created
                svc.ExtractTraceIdToLogContext();
                return svc;
            });

            services.AddHttpContextAccessor();

            return services;
        }
    }
}
