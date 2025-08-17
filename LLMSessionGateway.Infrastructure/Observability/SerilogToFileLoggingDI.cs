using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Observability.Shared.Contracts;
using Observability.Shared.DefaultImplementations;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public static class SerilogToFileLoggingDI
    {
        public static IServiceCollection AddSerilogToFileLogging(this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<Serilog.ILogger>(Log.Logger);
            services.AddScoped<IStructuredLogger, SerilogStructuredLogger>();

            var fileConfig = config
                .GetSection("Logging:File")
                .Get<FileLoggingConfig>();

            if (!Enum.TryParse<RollingInterval>(fileConfig!.RollingInterval, out var rollingInterval))
            {
                throw new InvalidOperationException($"Invalid RollingInterval: {fileConfig.RollingInterval}");
            }

            LoggingBootstrapper.ConfigureToFileSerilog(
                fileConfig.BasePath,
                fileConfig.FileNamePattern,
                rollingInterval
            );

            return services;
        }
    }
}
