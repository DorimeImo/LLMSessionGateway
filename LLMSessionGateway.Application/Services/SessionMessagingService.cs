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

        public async Task<Result<Unit>> SendMessageAsync(string sessionId, string message, CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingName = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingName))
            {
                return await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    ct => _chatBackend.SendUserMessageAsync(sessionId, message, ct),
                    ct,
                    onRetry: RetryLogger.LogRetry<Unit>(_logger, tracingName));
            }
        }

        public IAsyncEnumerable<string> StreamReplyAsync(string sessionId, CancellationToken ct = default)
        {
            return _chatBackend.StreamAssistantReplyAsync(sessionId, ct);
        }
    }
}
