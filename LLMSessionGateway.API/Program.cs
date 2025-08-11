using Asp.Versioning.Conventions;
using Asp.Versioning;
using LLMSessionGateway.API.Controllers;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using System.Text.Json;

namespace LLMSessionGateway.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Auth
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            // Infrastructure registration
            builder.Services
                .AddRedisActiveSessionStore(builder.Configuration)
                .AddAzureBlobArchiveStore(builder.Configuration)
                .AddGrpcChatBackend(builder.Configuration)
                .AddOpenTelemetryTracing(builder.Configuration)
                .AddSerilogToFileLogging(builder.Configuration)
                .AddPollyRetryPolicy(builder.Configuration)
                .AddGatewayHealthChecks(builder.Configuration);

            //Application services registration
            builder.Services.AddScoped<IChatSessionOrchestrator, ChatSessionOrchestrator>();

            //API Versioning
            builder.Services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;       
                options.ApiVersionReader = new UrlSegmentApiVersionReader(); // Versioning verification middleware
            }).AddMvc(mvc =>
            {
                mvc.Conventions.Controller<ChatController>()
                   .HasApiVersion(new ApiVersion(1, 0));
            }).AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";                
                options.SubstituteApiVersionInUrl = true;
            });

            //Swagger (OpenAPI)
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                // We’ll fill docs per version at runtime using the provider below
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "LLMSessionGateway API",
                    Version = "v1",
                    Description = "Session management and AI model routing endpoints (versioned)"
                });
            });

            // API pipeline
            builder.Services.AddControllers();

            var app = builder.Build();

            app.MapControllers();

            // Health Check: Liveness: cheap self-check
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => false,
            }).AllowAnonymous();

            // Health Check: Readiness: only checks tagged "ready"
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


