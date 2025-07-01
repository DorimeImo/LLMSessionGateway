using LLMSessionGateway.Application.Contracts.DTOs;
using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Application.Contracts.Observability;
using System.Net;
using System.Text.Json;

namespace LLMSessionGateway.API.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly IStructuredLogger _logger;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next, IStructuredLogger structuredLogger)
        {
            _next = next;
            _logger = structuredLogger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var errorCode = "UNHANDLED_ERROR";

            _logger.LogCritical(source, operation, "Unhandled exception.", exception);

            var errorResponse = new ErrorResponse
            {
                UserFriendlyMessage = "An unexpected error occurred. Please try again later.",
                ErrorMessage = exception.Message,
                ErrorCode = errorCode,
                IsRetryable = false,
                CorrelationId = _logger.Current.TraceId,
                Timestamp = DateTime.UtcNow
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            try
            {
                var json = JsonSerializer.Serialize(errorResponse);
                await context.Response.WriteAsync(json);
            }
            catch (Exception serializationEx)
            {
                _logger.LogError(source, operation, "Failed to serialize ErrorResponse.", serializationEx);

                await context.Response.WriteAsync(
                    $"{{\"message\":\"An unexpected error occurred.\",\"correlationId\":\"{_logger.Current.TraceId}\"}}");
            } 
        }
    }
}
