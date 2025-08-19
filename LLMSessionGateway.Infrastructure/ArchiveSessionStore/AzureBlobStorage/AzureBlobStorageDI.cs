using Azure.Identity;
using Azure.Storage.Blobs;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Observability.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.ArchiveSessionStore.AzureBlobStorage
{
    public static class AzureBlobStorageDI
    {
        public static IServiceCollection AddAzureBlobArchiveStore(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<AzureBlobConfigs>(config.GetSection("AzureBlob"));

            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AzureBlobConfigs>>().Value;

                var accountUrl = options.BlobAccountUrl!;          
                var credential = new DefaultAzureCredential();             
                return new BlobServiceClient(new Uri(accountUrl), credential);
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
    }
}
