using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public static class LoggingBootstrapper
    {
        public static void ConfigureToFileSerilog(string basePath, string fileNamePattern, RollingInterval rollingInterval)
        {
            var fullPath = Path.Combine(basePath, fileNamePattern);

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    fullPath,
                    rollingInterval: rollingInterval)
                .CreateLogger();
        }
    }
}
