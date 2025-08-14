using LLMSessionGateway.API.DTOs;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using System.Net;
using System.Text.Json;

namespace LLMSessionGateway.API.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        public GlobalExceptionHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var logger = context.RequestServices.GetRequiredService<IStructuredLogger>();

            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex, logger);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception, IStructuredLogger logger)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var errorCode = "UNHANDLED_ERROR";

            logger.LogCritical(source, operation, "Unhandled exception: " + exception.Message, exception);

            if (context.Response.HasStarted)
            {
                logger.LogError(source, operation, "Unhandled exception after response started.", exception);
                return;
            }

            var status = context.Response.StatusCode >= 400
                ? context.Response.StatusCode
                : (int)HttpStatusCode.InternalServerError;

            var errorResponse = new ErrorResponse
            {
                UserFriendlyMessage = "An unexpected error occurred. Please try again later.",
                ErrorMessage = "Internal error",
                ErrorCode = errorCode,
                IsRetryable = false,
                CorrelationId = logger.Current.TraceId,
                Timestamp = DateTime.UtcNow
            };

            context.Response.Clear();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = status;

            try
            {
                var json = JsonSerializer.Serialize(errorResponse);
                await context.Response.WriteAsync(json);
            }
            catch (Exception serializationEx)
            {
                logger.LogError(source, operation, "Failed to serialize ErrorResponse.", serializationEx);

                await context.Response.WriteAsync(
                    $"{{\"message\":\"An unexpected error occurred.\",\"correlationId\":\"{logger.Current.TraceId}\"}}");
            } 
        }
    }
}
