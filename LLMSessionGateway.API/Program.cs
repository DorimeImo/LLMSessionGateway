using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Infrastructure;

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
                .AddPollyRetryPolicy(builder.Configuration);

            // Application services
            builder.Services.AddScoped<IChatSessionOrchestrator, ChatSessionOrchestrator>();

            // API pipeline
            builder.Services.AddControllers();

            var app = builder.Build();

            app.MapControllers();

            app.Run();
        }
    }
}


