namespace LLMSessionGateway.API.Hosting
{
    public sealed class ApiLimitsOptions
    {
        public long MaxRequestBodySizeBytes { get; set; } = 1_048_576; // 1 MB
        public int JsonMaxDepth { get; set; } = 32;
    }
}


