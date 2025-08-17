using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Observability.Shared.Contracts;
using StackExchange.Redis;

namespace LLMSessionGateway.Infrastructure.ActiveSessionStore.Redis
{
    public static class RedisDI
    {
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
    }
}
