using LLMSessionGateway.Application.Contracts.KeyGeneration;
using LLMSessionGateway.Application.Contracts.Observability;

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
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            _tracingService.ExtractTraceIdToLogContext(tracingOperationName);

            await _next(context);

            _tracingService.InjectTraceContextIntoHttpResponse();
        }
    }
}
