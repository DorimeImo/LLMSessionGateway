using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Infrastructure.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;

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
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                    "AzureBlob:ConnectionString is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.ContainerName),
                    "AzureBlob:ContainerName is required.")
                .ValidateOnStart();

            // gRPC endpoint (production: TLS required)
            services.AddOptions<GrpcConfigs>()
                .Bind(config.GetSection("Grpc:ChatService"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Grpc:ChatService:Host is required.")
                .Validate(o => o.Port is > 0 and <= 65535, "Grpc:ChatService:Port must be 1..65535.")
                .Validate(o => o.UseTls, "In production, Grpc:ChatService:UseTls must be true.")
                .Validate(o =>
                    !o.EnableMtls ||
                    (!string.IsNullOrWhiteSpace(o.ClientCertificateBase64Env)
                     && !string.IsNullOrWhiteSpace(o.ClientCertificatePasswordEnv)),
                    "When EnableMtls=true, set ClientCertificateBase64Env and ClientCertificatePasswordEnv.")
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
            services.AddOptions<FileLoggingConfig>()
                .Bind(config.GetSection("Logging:File"))
                .ValidateDataAnnotations()
                .Validate(o => Enum.TryParse<RollingInterval>(o.RollingInterval, out _),
                    "Logging:File:RollingInterval must be a valid Serilog RollingInterval.")
                .ValidateOnStart();

            // OpenTelemetry
            services.AddOptions<JaegerConfigs>()
                .Bind(config.GetSection("OpenTelemetry:Jaeger"))
                .ValidateDataAnnotations()
                .Validate(o => o.AgentPort is > 0 and <= 65535,
                    "OpenTelemetry:Jaeger:AgentPort must be 1..65535.")
                .ValidateOnStart();

            return services;
        }

        

        

        

        

        

    }
}
