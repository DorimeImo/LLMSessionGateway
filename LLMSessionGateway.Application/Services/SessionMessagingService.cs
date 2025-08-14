using LLMSessionGateway.Application.Contracts.Commands;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Application.Helpers;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;

namespace LLMSessionGateway.Application.Services
{
    public class SessionMessagingService : ISessionMessagingService
    {
        private readonly IChatBackend _chatBackend;
        private readonly IRetryPolicyRunner _retryRunner;
        private readonly ITracingService _tracingService;
        private readonly IStructuredLogger _logger;

        public SessionMessagingService(
            IChatBackend chatBackend,
            IRetryPolicyRunner retryRunner,
            ITracingService tracingService,
            IStructuredLogger logger)
        {
            _chatBackend = chatBackend;
            _retryRunner = retryRunner;
            _tracingService = tracingService;
            _logger = logger;
        }

        public async Task<Result<Unit>> OpenConnectionAsync(string sessionId, string userId, CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingName = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingName))
            {
                return await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    ct => _chatBackend.OpenConnectionAsync(sessionId, userId, ct),
                    ct,
                    onRetry: RetryLogger.LogRetry<Unit>(_logger, tracingName));
            }
        }

        public async Task<Result<Unit>> SendMessageAsync(SendMessageCommand command, CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingName = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingName))
            {
                return await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    ct => _chatBackend.SendUserMessageAsync(command.SessionId, command.Message, command.MessageId, ct),
                    ct,
                    onRetry: RetryLogger.LogRetry<Unit>(_logger, tracingName));
            }
        }

        public IAsyncEnumerable<string> StreamReplyAsync(string sessionId, string messageId, CancellationToken ct = default)
        {
            return _chatBackend.StreamAssistantReplyAsync(sessionId, messageId, ct);
        }
    }
}
