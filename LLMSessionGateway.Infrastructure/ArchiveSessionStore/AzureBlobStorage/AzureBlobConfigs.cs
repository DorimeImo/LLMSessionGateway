using System.ComponentModel.DataAnnotations;

namespace LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis
{
    public class AzureBlobConfigs
    {
        [Required] public string ContainerName { get; set; } = default!;
        [Required] public string BlobAccountUrl { get; set; } = default!;
    }
}
