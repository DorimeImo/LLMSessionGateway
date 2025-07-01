using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Application.Contracts.Observability;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class OpenTelemetryTracingService : ITracingService
    {
        private static readonly ActivitySource ActivitySource = new("LLMSessionGateway");
        private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IStructuredLogger _logger;

        public OpenTelemetryTracingService(
            IHttpContextAccessor httpContextAccessor,
            IStructuredLogger structuredLogger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = structuredLogger;
        }

        public void ExtractTraceIdToLogContext(string operationName)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();

            var activity = Activity.Current;

            if (activity != null)
            {
                _logger.Current.TraceId = activity.TraceId.ToString();
            }
            else
            {
                _logger.LogWarning(
                    source,
                    operation,
                    "No active Activity found. Tracing might not be set up correctly or the request is missing trace headers.");
            }
        }

        public IDisposable? StartActivity(string tracingOperationName)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();

            var activity = ActivitySource.StartActivity(tracingOperationName, ActivityKind.Internal);

            if (activity == null)
            {
                _logger.LogWarning(source, operation, "Activity could not be started — no listener or sampling prevented it.");
            }

            return activity;
        }

        public void InjectTraceContextIntoHttpResponse()
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();

            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("HttpContext is null while attempting to inject trace context.");

            var activityContext = Activity.Current?.Context ?? default;
            if (activityContext == default)
            {
                _logger.LogWarning(source, operation, 
                    "No active Activity context found; trace context will not be injected into response.");
                return;
            }
                
            var propagationContext = new PropagationContext(activityContext, Baggage.Current);

            Propagator.Inject(propagationContext, httpContext.Response, static (response, key, value) =>
            {
                if (!response.Headers.ContainsKey(key))
                {
                    response.Headers[key] = value;
                }
            });
        }
    }
}
