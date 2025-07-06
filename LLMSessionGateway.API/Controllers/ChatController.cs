using LLMSessionGateway.Application.Contracts.DTOs;
using LLMSessionGateway.Application.Contracts.ErrorHandling;
using LLMSessionGateway.Application.Contracts.KeyGeneration;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core.Utilities.Functional;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using System.Security.Claims;

namespace LLMSessionGateway.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly ISessionManager _sessionManager;

        private readonly IStructuredLogger _logger;
        private readonly ITracingService _tracingService;

        public ChatController(ISessionManager sessionManager, IStructuredLogger structuredLogger, ITracingService tracingService)
        {
            _sessionManager = sessionManager;
            _logger = structuredLogger;
            _tracingService = tracingService;
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartSession(CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var userId = GetUserIdOrThrow();

                var result = await _sessionManager.StartSessionAsync(userId, cancellationToken);

                if (result.IsFailure)
                    return ToErrorResponse(result);

                var session = result.Value!;

                _logger.Current.SessionId = session.SessionId;

                return Ok(new SessionResponse
                {
                    SessionId = session.SessionId,
                    CreatedAt = session.CreatedAt
                });
            }
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromQuery] string sessionId, [FromBody] string message, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var result = await _sessionManager.SendMessageAsync(sessionId, message, cancellationToken);

                if (result.IsFailure)
                    return ToErrorResponse(result);

                return Ok();
            }  
        }

        [HttpGet("stream")]
        public async Task StreamReply([FromQuery] string sessionId, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                Response.Headers.Append("Content-Type", "text/event-stream");

                await foreach (var chunk in _sessionManager.StreamReplyAsync(sessionId, cancellationToken))
                {
                    await Response.WriteAsync($"data: {chunk}\n\n");
                    await Response.Body.FlushAsync();
                }
            }  
        }

        [HttpPost("end")]
        public async Task<IActionResult> EndSession([FromQuery] string sessionId)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var result = await _sessionManager.EndSessionAsync(sessionId);

                if (result.IsFailure)
                    return ToErrorResponse(result);

                return Ok();
            }
        }

        private IActionResult ToErrorResponse<T>(Result<T> result)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();;

            var userFriendlyMessage = UserErrorCodeCatalog.GetMessage(result.ErrorCode);
            if (!UserErrorCodeCatalog.IsMapped(result.ErrorCode))
            {
                _logger.LogError(
                    source,
                    operation,
                    $"Unmapped error code returned: {result.ErrorCode ?? "UNKNOWN"}"
                );
            }

            var error = new ErrorResponse
            {
                UserFriendlyMessage = userFriendlyMessage,
                ErrorMessage = result.Error ?? "Unknown error",
                ErrorCode = result.ErrorCode ?? "UNKNOWN",
                CorrelationId = _logger.Current.TraceId,
                Timestamp = DateTime.UtcNow
            };

            return StatusCode(500, error);
        }

        private string GetUserIdOrThrow()
        {
            var userId = User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("Unauthorized request: missing 'sub' claim.");
            return userId;
        }
    }
}
