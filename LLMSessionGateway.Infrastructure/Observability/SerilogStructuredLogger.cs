using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Application.Contracts.Observability;
using Serilog;
using Serilog.Events;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class SerilogStructuredLogger : IStructuredLogger
    {
        private static readonly AsyncLocal<LogContext?> _current = new();
        private readonly ILogger _logger;

        public SerilogStructuredLogger(ILogger logger)
        {
            _logger = logger;
        }

        public LogContext Current => _current.Value ??= new LogContext();

        public void Set(LogContext context)
        {
            _current.Value = context;
        }

        public void LogWarning(string source, string operation, string message, Exception? ex = null) =>
            Log(AppLogLevel.Warning, source, operation, message);

        public void LogError(string source, string operation, string message, Exception? ex = null) =>
            Log(AppLogLevel.Error, source, operation, message, ex);

        public void LogCritical(string source, string operation, string message, Exception? ex = null) =>
            Log(AppLogLevel.Critical, source, operation, message, ex);

        private void Log(AppLogLevel level, string source, string operation, string message, Exception? ex = null)
        {
            var ctx = Current;
            ctx.Source = source;
            ctx.Operation = operation;
            ctx.Message = message;
            ctx.Timestamp = DateTimeOffset.UtcNow;
            ctx.Exception = ex;
            ctx.Level = level;

            _logger
                .ForContext("CorrelationId", ctx.CorrelationId)
                .ForContext("TraceId", ctx.TraceId)
                .ForContext("Source", ctx.Source)
                .ForContext("Operation", ctx.Operation)
                .ForContext("UserId", ctx.UserId)
                .ForContext("SessionId", ctx.SessionId)
                .ForContext("Timestamp", ctx.Timestamp)
                .ForContext("Level", ctx.Level)
                .Write(MapLevel(level), ex, "{Message}", message);
        }

        private static LogEventLevel MapLevel(AppLogLevel level) => level switch
        {
            AppLogLevel.Warning => LogEventLevel.Warning,
            AppLogLevel.Error => LogEventLevel.Error,
            AppLogLevel.Critical => LogEventLevel.Fatal,
            AppLogLevel.None => LogEventLevel.Verbose,
            _ => LogEventLevel.Information
        };
    }
}
