using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Application.Helpers;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using System.Threading;

namespace LLMSessionGateway.Application.Services
{
    public class SessionLifecycleService : ISessionLifecycleService
    {
        private readonly IActiveSessionStore _activeSessionStore;
        private readonly IArchiveSessionStore _archiveSessionStore;
        private readonly IRetryPolicyRunner _retryRunner;
        private readonly ITracingService _tracingService;
        private readonly IStructuredLogger _logger;

        public SessionLifecycleService(
            IActiveSessionStore activeSessionStore,
            IArchiveSessionStore archiveSessionStore,
            IRetryPolicyRunner retryRunner,
            ITracingService tracingService,
            IStructuredLogger logger)
        {
            _activeSessionStore = activeSessionStore;
            _archiveSessionStore = archiveSessionStore;
            _retryRunner = retryRunner;
            _tracingService = tracingService;
            _logger = logger;
        }

        public async Task<Result<ChatSession>> StartSessionAsync(string userId, CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var sessionIdResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<string>(
                    ct => _activeSessionStore.GetActiveSessionIdAsync(userId, ct),
                    ct,
                    onRetry: RetryLogger.LogRetry<string>(_logger, tracingOperationName));

                if (sessionIdResult.IsSuccess)
                {
                    var sessionResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                        ct => _activeSessionStore.GetSessionAsync(sessionIdResult.Value!, ct),
                        ct,
                        onRetry: RetryLogger.LogRetry<ChatSession>(_logger, tracingOperationName));

                    if (sessionResult.IsSuccess)
                        return Result<ChatSession>.Success(sessionResult.Value!);
                }

                var session = new ChatSession
                {
                    SessionId = Guid.NewGuid().ToString(),
                    UserId = userId
                };

                var saveResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    ct => _activeSessionStore.SaveSessionAsync(session, ct),
                    ct,
                    onRetry: RetryLogger.LogRetry<Unit>(_logger, tracingOperationName));

                return saveResult.IsSuccess
                    ? Result<ChatSession>.Success(session)
                    : Result<ChatSession>.Failure(saveResult.Error!, saveResult.ErrorCode, saveResult.IsRetryable);
            }
        }

        public async Task<Result<Unit>> EndSessionAsync(string sessionId, CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var sessionResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                    _ => _activeSessionStore.GetSessionAsync(sessionId),
                    CancellationToken.None,
                    onRetry: RetryLogger.LogRetry<ChatSession>(_logger, tracingOperationName));

                if (sessionResult.IsFailure || sessionResult.Value is null)
                    return Result<Unit>.Success(Unit.Value);

                var archiveResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    _ => _archiveSessionStore.PersistSessionAsync(sessionResult.Value!),
                    CancellationToken.None,
                    onRetry: RetryLogger.LogRetry<Unit>(_logger, tracingOperationName));

                if (archiveResult.IsFailure)
                    return archiveResult;

                var deleteResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    _ => _activeSessionStore.DeleteSessionAsync(sessionResult.Value!),
                    CancellationToken.None,
                    onRetry: RetryLogger.LogRetry<Unit>(_logger, tracingOperationName));

                return deleteResult;
            }
        }
    }
}
