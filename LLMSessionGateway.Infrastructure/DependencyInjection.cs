using Azure.Storage.Blobs;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Infrastructure.HealthChecks;
using LLMSessionGateway.Infrastructure.Observability;
using LLMSessionGateway.Infrastructure.ActiveSessionStore;
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
using LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;

namespace LLMSessionGateway.Infrastructure
{
    public static class DependencyInjection
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

            // gRPC endpoint
            services.AddOptions<GrpcConfigs>()
                .Bind(config.GetSection("Grpc:ChatService"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Grpc:ChatService:Host is required.")
                .Validate(o => o.Port is > 0 and <= 65535, "Grpc:ChatService:Port must be 1..65535.")
                .ValidateOnStart();

            // gRPC per-call timeouts
            services.AddOptions<GrpcTimeoutsOptions>()
                .Bind(config.GetSection("Grpc:Timeouts"))
                .Validate(o => o.OpenSession > TimeSpan.Zero, "Grpc:Timeouts:OpenSession must be > 0.")
                .Validate(o => o.SendMessage > TimeSpan.Zero, "Grpc:Timeouts:SendMessage must be > 0.")
                .Validate(o => o.StreamReplySetup > TimeSpan.Zero, "Grpc:Timeouts:StreamReplySetup must be > 0.")
                .Validate(o => o.CloseSession > TimeSpan.Zero, "Grpc:Timeouts:CloseSession must be > 0.")
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

        public static IServiceCollection AddRedisActiveSessionStore(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<RedisConfigs>(config.GetSection("Redis"));

            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(
                    sp.GetRequiredService<IOptions<RedisConfigs>>().Value.ConnectionString));

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

                return new BlobServiceClient(options.ConnectionString);
            });

            services.AddScoped<IArchiveSessionStore>(sp =>
            {
                var blobServiceClient = sp.GetRequiredService<BlobServiceClient>();
                var logger = sp.GetRequiredService<IStructuredLogger>();
                var tracer = sp.GetRequiredService<ITracingService>();
                var config = sp.GetRequiredService<IConfiguration>();

                var containerName = config.GetSection("AzureBlob")["ContainerName"];

                return new AzureBlobArchiveStore(blobServiceClient, containerName!, logger, tracer);
            });

            return services;
        }

        public static IServiceCollection AddGrpcChatBackend(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<GrpcConfigs>(config.GetSection("Grpc:ChatService"));
            services.Configure<GrpcTimeoutsOptions>(config.GetSection("Grpc:Timeouts"));

            var grpcConfigs = config.GetSection("Grpc:ChatService").Get<GrpcConfigs>();

            var grpcAddress = $"http://{grpcConfigs!.Host}:{grpcConfigs.Port}";

            services.AddGrpcClient<ChatService.ChatServiceClient>(o =>
            {
                o.Address = new Uri(grpcAddress);
            });

            services.AddScoped<IChatBackend, GrpcChatBackend>();

            return services;
        }

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
