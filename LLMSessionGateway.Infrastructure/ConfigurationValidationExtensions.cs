using LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;
using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace LLMSessionGateway.Infrastructure
{
    public static class ConfigurationValidationExtensions
    {
        public static IServiceCollection AddConfigurationValidation(
            this IServiceCollection services, IConfiguration config)
        {
            // Redis
            services.AddOptions<RedisConfigs>()
                .Bind(config.GetSection("Redis"))
                .ValidateDataAnnotations()
                .Validate(o => o.LockTtlSeconds > 0, "Redis:LockTtlSeconds must be > 0.")
                .Validate(o => o.ActiveSessionTtlSeconds > 0, "Redis:ActiveSessionTtlSeconds must be > 0.")
                .ValidateOnStart();

            // Azure Blob
            services.AddOptions<AzureBlobConfigs>()
                .Bind(config.GetSection("AzureBlob"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.BlobAccountUrl),
                    "AzureBlob:AccountUrl is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName),
                    "AzureBlob:ContainerName is required.")
                .ValidateOnStart();

            // gRPC endpoint (production: TLS required)
            services.AddOptions<GrpcConfigs>()
                .Bind(config.GetSection("Grpc:ChatService"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Grpc:ChatService:Host is required.")
                .Validate(o => o.Port is > 0 and <= 65535, "Grpc:ChatService:Port must be 1..65535.")
                .Validate<IHostEnvironment>(
                    (o, env) => env.IsDevelopment() || o.UseTls,
                    "Grpc:ChatService:UseTls must be true outside Development.")
                .Validate(o =>
                    !o.EnableMtls ||
                    (!string.IsNullOrWhiteSpace(o.ClientCertificateBase64Env)
                     && !string.IsNullOrWhiteSpace(o.ClientCertificatePasswordEnv)),
                    "When EnableMtls=true, set ClientCertificateBase64Env and ClientCertificatePasswordEnv.")
                .Validate(o => IsValidScope(o.Scope),
                    "Grpc:ChatService:Scope must be an absolute URI like 'api://app/.default' or 'api://app/read'.")
                .ValidateOnStart();

            // gRPC per-call timeouts/sizes (note: type is GrpcTimeoutsOptions)
            services.AddOptions<GrpcTimeoutsConfigs>()
                .Bind(config.GetSection("Grpc:Timeouts"))
                .Validate(o => o.OpenSeconds > 0, "Grpc:Timeouts:OpenSeconds must be > 0.")
                .Validate(o => o.SendSeconds > 0, "Grpc:Timeouts:SendSeconds must be > 0.")
                .Validate(o => o.StreamSetupSeconds > 0, "Grpc:Timeouts:StreamSetupSeconds must be > 0.")
                .Validate(o => o.CloseSeconds > 0, "Grpc:Timeouts:CloseSeconds must be > 0.")
                .Validate(o => o.MaxSendBytes > 0 && o.MaxReceiveBytes > 0,
                          "Grpc:Timeouts:MaxSendBytes and MaxReceiveBytes must be > 0.")
                .Validate(o => o.MaxReceiveBytes >= o.MaxSendBytes,
                          "Grpc:Timeouts:MaxReceiveBytes must be >= MaxSendBytes.")
                .ValidateOnStart();

            // Logging
            services.AddOptions<LogToAppInsightsConfig>()
                .Bind(config.GetSection("Logging:File"))
                .ValidateDataAnnotations()
                .Validate(o => Enum.TryParse<RollingInterval>(o.RollingInterval, out _),
                    "Logging:File:RollingInterval must be a valid Serilog RollingInterval.")
                .ValidateOnStart();

            // OpenTelemetry
            services.AddOptions<AzureAppInsightsConfigs>()
                .Bind(config.GetSection("OpenTelemetry:ApplicationInsights"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                    "OpenTelemetry:ApplicationInsights:ConnectionString is required.")
                .ValidateOnStart();

            return services;
        }

        private static bool IsValidScope(string? scope)
        {
            var s = scope?.Trim();
            if (string.IsNullOrWhiteSpace(s))
                return false;

            if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
                return false;

            if (!(uri.Scheme.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                  uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                return false;

            if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
                return false;

            return true;
        }
    }
}
