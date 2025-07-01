namespace LLMSessionGateway.Application.Contracts.Logging
{
    public interface IStructuredLogger
    {
        LogContext Current { get; }
        void Set(LogContext context);
        void LogWarning(string source, string operation, string message, Exception? ex = null);
        void LogError(string source, string operation, string message, Exception? ex = null);
        void LogCritical(string source, string operation, string message, Exception? ex = null);
    }
}
