using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace LLMSessionGateway.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            // Infrastructure registrations
            builder.Services
                .AddRedisActiveSessionStore(builder.Configuration)
                .AddAzureBlobArchiveStore(builder.Configuration)
                .AddGrpcChatBackend(builder.Configuration)
                .AddOpenTelemetryTracing(builder.Configuration)
                .AddSerilogToFileLogging(builder.Configuration)
                .AddPollyRetryPolicy(builder.Configuration)
                .AddGatewayHealthChecks(builder.Configuration);

            // Application services
            builder.Services.AddScoped<IChatSessionOrchestrator, ChatSessionOrchestrator>();

            // API pipeline
            builder.Services.AddControllers();

            var app = builder.Build();

            app.MapControllers();

            // Liveness: cheap self-check
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => false,
            }).AllowAnonymous();

            // Readiness: only checks tagged "ready" (Redis, Blob, gRPC)
            app.MapHealthChecks("/ready", new HealthCheckOptions
            {
                Predicate = reg => reg.Tags.Contains("ready"),
                ResponseWriter = async (ctx, report) =>
                {
                    ctx.Response.ContentType = "application/json";
                    var payload = new
                    {
                        status = report.Status.ToString(),
                        checks = report.Entries.Select(e => new
                        {
                            name = e.Key,
                            status = e.Value.Status.ToString(),
                            description = e.Value.Description,
                            durationMs = e.Value.Duration.TotalMilliseconds
                        }),
                        timestamp = DateTime.UtcNow
                    };
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
                }
            }).AllowAnonymous();

            app.Run();
        }
    }
}


