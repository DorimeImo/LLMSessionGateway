using Observability.Shared.Contracts;
using Observability.Shared.Helpers;

namespace LLMSessionGateway.API.Middleware
{
    public class ObservabilityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ITracingService _tracingService;

        public ObservabilityMiddleware(RequestDelegate next, ITracingService tracingService)
        {
            _next = next;
            _tracingService = tracingService;
        }

        public async Task Invoke(HttpContext context)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            _tracingService.ExtractTraceIdToLogContext(tracingOperationName);

            await _next(context);
        }
    }
}
