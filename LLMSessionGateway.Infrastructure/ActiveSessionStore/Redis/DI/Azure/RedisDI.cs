using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Infrastructure.ActiveSessionStore.Redis;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Observability.Shared.Contracts;
using StackExchange.Redis;

namespace LLMSessionGateway.Infrastructure.ActiveSessionStore.Redis.DI.Azure
{
    public static class RedisDI
    {
        public static IServiceCollection AddAzureRedisActiveSessionStore(this IServiceCollection services, IConfiguration config)
        {
            ValidateAndAddConfigs(services, config);

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RedisConfigs>>().Value;

                var cfg = sp.GetRequiredService<IConfiguration>();
                var conn = Environment.GetEnvironmentVariable("AZURE_REDIS_CONNECTION_STRING")
                    ?? throw new InvalidOperationException("Azure Redis connection string not provided. Set AZURE_REDIS_CONNECTION_STRING.");

                return ConnectionMultiplexer.Connect(conn);
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

        private static void ValidateAndAddConfigs(IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<RedisConfigs>()
                .Bind(config.GetSection("Redis"))
                .ValidateDataAnnotations()
                .Validate(o => o.LockTtlSeconds > 0, "Redis:LockTtlSeconds must be > 0.")
                .Validate(o => o.ActiveSessionTtlSeconds > 0, "Redis:ActiveSessionTtlSeconds must be > 0.")
                .ValidateOnStart();
        }
    }
}
