using Azure.Monitor.OpenTelemetry.Exporter;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Observability.Shared.Contracts;
using Observability.Shared.DefaultImplementations;
using Serilog;
using Serilog.Core;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public static class AzureLoggingDI
    {
        public static IServiceCollection AddAzureLogging(this IServiceCollection services, IConfiguration config)
        {
            ValidateAndAddConfigs(services, config);

            services.AddSingleton<Serilog.ILogger>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<LogConfigs>>().Value;

                var connStr =
                    Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")
                    ?? throw new InvalidOperationException("Missing Azure Application Insights connection string. Set APPLICATIONINSIGHTS_CONNECTION_STRING.");

                var rolling = TryMapRollingInterval(options!.RollingInterval, out var ri) ? ri : Serilog.RollingInterval.Day;

                Log.Logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.File(
                        path: options!.FileNamePattern,
                        rollingInterval: rolling,
                        shared: true)
                    .WriteTo.ApplicationInsights(
                        connectionString: connStr,
                        telemetryConverter: TelemetryConverter.Traces)
                    .CreateLogger();

                return Log.Logger;
            });

            services.AddScoped<IStructuredLogger, SerilogStructuredLogger>();
            return services;
        }

        private static void ValidateAndAddConfigs(IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<LogConfigs>()
                .Bind(config.GetSection("Logging:LogConfigs"))
                .ValidateDataAnnotations()
                .Validate(o => TryMapRollingInterval(o.RollingInterval, out _),
                    "Logging:LogConfigs:RollingInterval must be one of: Infinite, Year, Month, Day, Hour, Minute.")
                .Validate(o => IsValidFilePattern(o.FileNamePattern),
                    "Logging:LogConfigs:FileNamePattern is invalid. Use tokens {Year},{Month},{Day},{Hour},{Minute},{Second}; avoid invalid path chars; and do not end with a separator.")
                .ValidateOnStart();
        }

        private static bool TryMapRollingInterval(string? s, out Serilog.RollingInterval value) =>
        Enum.TryParse(s, ignoreCase: true, out value);

        private static bool IsValidFilePattern(string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;
            var p = pattern.Trim();

            // 1) Must not end with a directory separator
            if (p.EndsWith('/') || p.EndsWith('\\')) return false;

            // 2) Check for invalid path/file characters per segment
            var invalidPath = Path.GetInvalidPathChars();
            var invalidName = Path.GetInvalidFileNameChars();

            // Split into segments; last one is a file name
            var segments = p.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return false;

            // All segments must be path-valid; last segment must also be filename-valid
            for (int i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                if (seg.IndexOfAny(invalidPath) >= 0) return false;
                if (i == segments.Length - 1 && seg.IndexOfAny(invalidName) >= 0) return false;
            }

            return true;
        }
    }
}
