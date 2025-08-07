using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Application.Helpers;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;

namespace LLMSessionGateway.Application.Services
{
    public class ChatSessionUpdater : IChatSessionUpdater
    {
        private readonly IActiveSessionStore _activeSessionStore;
        private readonly IChatSessionService _sessionService;
        private readonly IRetryPolicyRunner _retryRunner;
        private readonly ITracingService _tracingService;
        private readonly IStructuredLogger _logger;

        public ChatSessionUpdater(
            IActiveSessionStore activeSessionStore,
            IChatSessionService sessionService,
            IRetryPolicyRunner retryRunner,
            ITracingService tracingService,
            IStructuredLogger logger)
        {
            _activeSessionStore = activeSessionStore;
            _sessionService = sessionService;
            _retryRunner = retryRunner;
            _tracingService = tracingService;
            _logger = logger;
        }

        public async Task<Result<Unit>> AddMessageAsync(string sessionId, ChatRole role, string content, CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperation = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperation))
            {
                var sessionResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                    ct => _activeSessionStore.GetSessionAsync(sessionId, ct),
                    ct,
                    onRetry: RetryLogger.LogRetry<ChatSession>(_logger, tracingOperation)
                );

                if (sessionResult.IsFailure || sessionResult.Value is null)
                    return Result<Unit>.Failure("Session not found", errorCode: "session_not_found");

                var session = sessionResult.Value;

                _sessionService.AddMessage(session, role, content);

                var updateResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    ct => _activeSessionStore.SaveSessionAsync(session, ct),
                    ct,
                    onRetry: RetryLogger.LogRetry<Unit>(_logger, tracingOperation)
                );

                return updateResult;
            }
        }
    }
}
