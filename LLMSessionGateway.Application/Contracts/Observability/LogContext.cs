
using LLMSessionGateway.Application.Contracts.Observability;

namespace LLMSessionGateway.Application.Contracts.Logging
{
    public class LogContext
    {
        public string? CorrelationId { get; set; }
        public string? TraceId { get; set; }
        public string? Source { get; set; }
        public string? Operation { get; set; }
        public string? UserId { get; set; }
        public string? SessionId { get; set; }
        public string? Message { get; set; }
        public AppLogLevel? Level { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public Exception? Exception { get; set; }
    }
}
