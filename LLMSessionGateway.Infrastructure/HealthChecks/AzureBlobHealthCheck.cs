using Azure.Storage.Blobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.HealthChecks
{
    public class AzureBlobHealthCheck : IHealthCheck
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        public AzureBlobHealthCheck(BlobServiceClient blobServiceClient, string containerName)
        {
            _blobServiceClient = blobServiceClient;
            _containerName = containerName;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                var container = _blobServiceClient.GetBlobContainerClient(_containerName);
                var exists = await container.ExistsAsync(ct);
                return exists.Value
                    ? HealthCheckResult.Healthy("Blob container exists")
                    : HealthCheckResult.Unhealthy($"Blob container '{_containerName}' not found");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Blob service unreachable", ex);
            }
        }
    }
}