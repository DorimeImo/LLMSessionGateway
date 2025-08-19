using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using LLMSessionGateway.API.Auth;
using LLMSessionGateway.API.Controllers;
using LLMSessionGateway.API.Hosting;
using LLMSessionGateway.API.Validation;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Infrastructure;
using LLMSessionGateway.Infrastructure.ActiveSessionStore.Redis;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.AzureBlobStorage;
using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Infrastructure.HealthChecks;
using LLMSessionGateway.Infrastructure.Observability;
using LLMSessionGateway.Infrastructure.Resilience;
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
            builder.Services.AddApiAuthenticationAndAuthorization(builder.Configuration);

            //Infrastructure registration
            builder.Services
                .AddConfigurationValidation(builder.Configuration)
                .AddRedisActiveSessionStore(builder.Configuration)
                .AddAzureBlobArchiveStore(builder.Configuration)
                .AddGrpcChatBackend(builder.Configuration)
                .AddOpenTelemetryToAzureMonitorTracing(builder.Configuration)
                .AddSerilogToAzureAppInsights(builder.Configuration)
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
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "LLMSessionGateway API",
                    Version = "v1",
                    Description = "Session management and AI model routing endpoints (versioned)"
                });
            });

            //API pipeline
            builder.Services.AddControllers();

            //API Request Validation
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddValidatorsFromAssemblyContaining<SendMessageRequestValidator>();
            builder.Services.AddValidatorsFromAssemblyContaining<StreamReplyRequestValidator>();

            //API Request Limits
            builder.Services
                .AddApiLimits(builder.Configuration);

            var app = builder.Build();

            app.UseRouting();

            //Auth
            app.UseApiAuth();

            //Middleware
            app.UseGlobalExceptionHandling();

            app.MapControllers();

            //Health Check: Liveness: cheap self-check
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = _ => false,
            }).AllowAnonymous();

            //Health Check: Readiness: only checks tagged "ready"
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


