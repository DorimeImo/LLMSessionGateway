using LLMSessionGateway.Application.Contracts.Resilience;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;

namespace LLMSessionGateway.Application.Helpers
{
    public static class RetryLogger
    {
        public static Func<RetryContext<T>, ValueTask> LogRetry<T>(
            IStructuredLogger logger,
            string operationName)
        {
            return ctx =>
            {
                var (source, operation) = CallerInfo.GetCallerClassAndMethod();
                var message = $"Retry #{ctx.Attempt} after {ctx.Delay.TotalMilliseconds}ms in '{operationName}'. " +
                              $"Error: {ctx.Result.Error}";
                logger.LogWarning(source, operation, message);
                return ValueTask.CompletedTask;
            };
        }
    }
}
