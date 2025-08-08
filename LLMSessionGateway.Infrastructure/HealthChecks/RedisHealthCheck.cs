using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.HealthChecks
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _redis;
        public RedisHealthCheck(IConnectionMultiplexer redis) => _redis = redis;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                var db = _redis.GetDatabase();
                var latency = await db.PingAsync();
                return HealthCheckResult.Healthy($"Redis OK ({latency.TotalMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis unreachable", ex);
            }
        }
    }
}
