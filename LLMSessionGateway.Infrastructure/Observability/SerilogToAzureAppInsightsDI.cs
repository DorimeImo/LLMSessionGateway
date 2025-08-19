using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Observability.Shared.Contracts;
using Observability.Shared.DefaultImplementations;
using Serilog;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public static class SerilogToAzureAppInsightsDI
    {
        public static IServiceCollection AddSerilogToAzureAppInsights(this IServiceCollection services, IConfiguration config)
        {
            var options = config.GetSection("Logging:ApplicationInsights").Get<LogToAppInsightsConfig>();

            var connStr =
                Environment.GetEnvironmentVariable(options!.AppInsightsConnectionString)
                ?? "APPLICATIONINSIGHTS_CONNECTION_STRING"
                ?? throw new InvalidOperationException(
                    "Missing APPLICATIONINSIGHTS_CONNECTION_STRING for App Insights logging.");

            var telemetryConfig = new TelemetryConfiguration
            {
                ConnectionString = connStr
            };

            Log.Logger = new LoggerConfiguration()       
                .Enrich.FromLogContext()               
                .WriteTo.ApplicationInsights(
                    telemetryConfiguration: telemetryConfig,
                    telemetryConverter: TelemetryConverter.Traces)
                .CreateLogger();

            services.AddSingleton<Serilog.ILogger>(Log.Logger);
            services.AddScoped<IStructuredLogger, SerilogStructuredLogger>();
            return services;
        }
    }
}
