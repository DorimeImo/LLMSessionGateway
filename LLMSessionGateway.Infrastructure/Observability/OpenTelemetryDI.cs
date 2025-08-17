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
        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, IConfiguration config)
        {
            var jaegerConfig = config.GetSection("OpenTelemetry:Jaeger").Get<JaegerConfigs>();

            services.AddOpenTelemetry()
                .WithTracing(builder =>
                {
                    builder
                        .AddAspNetCoreInstrumentation() //incoming requests
                        .AddHttpClientInstrumentation() //outgoing requests
                        .AddGrpcClientInstrumentation() //outgoing requests
                        .AddSource("LLMSessionGateway")
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("LLMSessionGateway"));


                    builder.AddConsoleExporter();

                    builder.AddJaegerExporter(o =>
                    {
                        o.AgentHost = jaegerConfig!.AgentHost;
                        o.AgentPort = jaegerConfig.AgentPort;
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
