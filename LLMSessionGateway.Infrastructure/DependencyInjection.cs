using Azure.Storage.Blobs;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Infrastructure.AzureBlobStorage;
using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Infrastructure.HealthChecks;
using LLMSessionGateway.Infrastructure.Observability;
using LLMSessionGateway.Infrastructure.Redis;
using LLMSessionGateway.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Observability.Shared.Contracts;
using Observability.Shared.DefaultImplementations;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

namespace LLMSessionGateway.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddRedisActiveSessionStore(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<RedisConfigs>(config.GetSection("Redis"));

            var connectionString = config.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("Redis connection string is not configured.");

            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(connectionString));

            services.AddScoped<IDistributedLockManager>(sp =>
            {
                var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                var options = sp.GetRequiredService<IOptions<RedisConfigs>>().Value;
                var logger = sp.GetRequiredService<IStructuredLogger>();
                var tracing = sp.GetRequiredService<ITracingService>();

                return new RedisLockManager(
                    redis,
                    logger,
                    tracing,
                    TimeSpan.FromSeconds(options.LockTtlSeconds));
            });

            services.AddScoped<IActiveSessionStore>(sp =>
            {
                var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                var options = sp.GetRequiredService<IOptions<RedisConfigs>>().Value;
                var logger = sp.GetRequiredService<IStructuredLogger>();
                var tracing = sp.GetRequiredService<ITracingService>();
                var lockManager = sp.GetRequiredService<IDistributedLockManager>();

                return new RedisActiveStore(
                    redis,
                    TimeSpan.FromSeconds(options.ActiveSessionTtlSeconds),
                    logger,
                    tracing,
                    lockManager);
            });

            return services;
        }

        public static IServiceCollection AddAzureBlobArchiveStore(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<AzureBlobConfigs>(config.GetSection("AzureBlob"));

            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureBlobConfigs>>().Value;

                if (string.IsNullOrWhiteSpace(options.ConnectionString))
                    throw new InvalidOperationException("Azure Blob connection string is not configured.");

                return new BlobServiceClient(options.ConnectionString);
            });

            services.AddScoped<IArchiveSessionStore>(sp =>
            {
                var blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
                var logger = sp.GetRequiredService<IStructuredLogger>();
                var tracer = sp.GetRequiredService<ITracingService>();
                var config = sp.GetRequiredService<IConfiguration>();

                var containerName = config.GetSection("AzureBlob")["ContainerName"]
                    ?? throw new InvalidOperationException("Container name not configured.");

                return new AzureBlobArchiveStore(blobServiceClient, containerName, logger, tracer);
            });

            return services;
        }

        public static IServiceCollection AddGrpcChatBackend(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<GrpcConfigs>(config.GetSection("Grpc:ChatService"));

            var grpcConfigs = config.GetSection("Grpc:ChatService").Get<GrpcConfigs>()
                ?? throw new InvalidOperationException("GrpcChatService connection string is not configured.");

            var grpcAddress = $"http://{grpcConfigs.Host}:{grpcConfigs.Port}";

            services.AddGrpcClient<ChatService.ChatServiceClient>(o =>
            {
                o.Address = new Uri(grpcAddress);
            });

            services.AddScoped<IChatBackend, GrpcChatBackend>();

            return services;
        }

        public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<ITracingService, OpenTelemetryTracingService>();

            var jaegerConfig = config.GetSection("OpenTelemetry:Jaeger").Get<JaegerConfigs>()
                ?? throw new InvalidOperationException("OpenTelemetry:Jaeger config is missing.");

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
                        o.AgentHost = jaegerConfig.AgentHost;
                        o.AgentPort = jaegerConfig.AgentPort;
                    });
                });

            services.AddHttpContextAccessor();

            return services;
        }

        public static IServiceCollection AddSerilogToFileLogging(this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<Serilog.ILogger>(Log.Logger);
            services.AddScoped<IStructuredLogger, SerilogStructuredLogger>();

            var fileConfig = config
                .GetSection("Logging:File")
                .Get<FileLoggingConfig>()
                ?? throw new InvalidOperationException("Logging:File configuration section is missing.");

            if (!Enum.TryParse<RollingInterval>(fileConfig.RollingInterval, out var rollingInterval))
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

        public static IServiceCollection AddPollyRetryPolicy(this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<IRetryPolicyRunner, RetryPolicies>();
            return services;
        }

        public static IServiceCollection AddGatewayHealthChecks(this IServiceCollection services, IConfiguration config)
        {
            services.AddHealthChecks()
                .Add(new HealthCheckRegistration(
                        "redis",
                        sp => new RedisHealthCheck(
                            sp.GetRequiredService<IConnectionMultiplexer>()
                        ),
                        failureStatus: HealthStatus.Unhealthy,
                        tags: new[] { "ready" }))
                .Add(new HealthCheckRegistration(
                        "azureBlob",
                        sp => new AzureBlobHealthCheck(
                            sp.GetRequiredService<BlobServiceClient>(),
                            sp.GetRequiredService<IOptions<AzureBlobConfigs>>().Value.ContainerName),
                        failureStatus: HealthStatus.Unhealthy,
                        tags: new[] { "ready" }))
                .Add(new HealthCheckRegistration(
                        "grpcEndpoint",
                        sp =>
                        {
                            var cfg = sp.GetRequiredService<IOptions<GrpcConfigs>>().Value;
                            return new GrpcEndpointHealthCheck(cfg.Host, cfg.Port, timeoutMs: 800);
                        },
                        failureStatus: HealthStatus.Unhealthy,
                        tags: new[] { "ready" }));

            return services;
        }
    }
}
