using Azure.Storage.Blobs;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Infrastructure.AzureBlobStorage;
using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Infrastructure.Observability;
using LLMSessionGateway.Infrastructure.Redis;
using LLMSessionGateway.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            services.AddScoped<IActiveSessionStore>(sp =>
            {
                var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                var options = sp.GetRequiredService<IOptions<RedisConfigs>>().Value;
                var logger = sp.GetRequiredService<IStructuredLogger>();
                var tracing = sp.GetRequiredService<ITracingService>();

                return new RedisActiveStore(
                    redis,
                    TimeSpan.FromSeconds(options.ActiveSessionTtlSeconds),
                    logger,
                    tracing);
            });

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

            return services;
        }

        public static IServiceCollection AddAzureBlobArchiveStore(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<AzureBlobConfigs>(config.GetSection("AzureBlob"));

            var connectionString = config.GetConnectionString("AzureBlob")
                ?? throw new InvalidOperationException("Azure Blob connection string is not configured.");

            services.AddSingleton(sp => new BlobServiceClient(connectionString));

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
            var grpcAddress = config.GetConnectionString("GrpcChatService")
                ?? throw new InvalidOperationException("GrpcChatService connection string is not configured.");

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
    }
}
