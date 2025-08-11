namespace LLMSessionGateway.API.DTOs
{
    public class ErrorResponse
    {
        public string UserFriendlyMessage { get; set; } = default!;
        public string ErrorMessage { get; set; } = default!;
        public string ErrorCode { get; set; } = default!;
        public bool IsRetryable { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

