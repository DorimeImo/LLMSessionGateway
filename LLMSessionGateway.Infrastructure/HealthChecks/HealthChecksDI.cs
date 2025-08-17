using Azure.Storage.Blobs;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;
using LLMSessionGateway.Infrastructure.Grpc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.HealthChecks
{
    public static class HealthChecksDI
    {
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
