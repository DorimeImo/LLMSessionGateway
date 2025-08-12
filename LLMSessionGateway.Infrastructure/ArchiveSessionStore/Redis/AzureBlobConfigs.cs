namespace LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis
{
    public class AzureBlobConfigs
    {
        public string ContainerName { get; set; } = default!;
        public string ConnectionString { get; set; } = default!;
    }
}
