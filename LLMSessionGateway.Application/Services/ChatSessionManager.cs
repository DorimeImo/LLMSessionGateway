using LLMSessionGateway.Application.Contracts.KeyGeneration;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using System.Runtime.CompilerServices;

namespace LLMSessionGateway.Services.Application
{
    public class ChatSessionManager : ISessionManager
    {
        private readonly IChatBackend _chatBackend;
        private readonly IActiveSessionStore _activeSessionStore;
        private readonly IArchiveSessionStore _archiveSessionStore;
        private readonly IDistributedLockManager _lockManager;

        private readonly IRetryPolicyRunner _retryRunner;
        private readonly ITracingService _tracingService;
        private readonly IStructuredLogger _logger;

        public ChatSessionManager(
            IChatBackend chatBackend,
            IActiveSessionStore activeSessionStore,
            IArchiveSessionStore archiveSessionStore,
            IDistributedLockManager lockManager,
            IRetryPolicyRunner retryRunner,
            ITracingService tracingService,
            IStructuredLogger logger)
        {
            _chatBackend = chatBackend;
            _activeSessionStore = activeSessionStore;
            _archiveSessionStore = archiveSessionStore;
            _lockManager = lockManager;

            _retryRunner = retryRunner;
            _tracingService = tracingService;
            _logger = logger;
        }

        public async Task<Result<ChatSession>> StartSessionAsync(string userId, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var sessionIdResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<string>(
                    ct => _activeSessionStore.GetActiveSessionIdAsync(userId, ct),
                    cancellationToken, 
                    onRetry: LogRetry<string>(tracingOperationName));

                if (sessionIdResult.IsSuccess)
                {
                    var sessionResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                        ct => _activeSessionStore.GetSessionAsync(sessionIdResult.Value!, ct),
                        cancellationToken,
                        onRetry: LogRetry<ChatSession>(tracingOperationName));

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
                    cancellationToken,
                    onRetry: LogRetry<Unit>(tracingOperationName));

                if (saveResult.IsFailure)
                    return Result<ChatSession>.Failure(saveResult.Error!, saveResult.ErrorCode);

                var backendResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    ct => _chatBackend.OpenConnectionAsync(session.SessionId, session.UserId, ct),
                    cancellationToken,
                    onRetry: LogRetry<Unit>(tracingOperationName));

                if (backendResult.IsFailure)
                    return Result<ChatSession>.Failure(backendResult.Error!, backendResult.ErrorCode);

                return Result<ChatSession>.Success(session);
            }
        }

        public async Task<Result<Unit>> SendMessageAsync(string sessionId, string message, CancellationToken cancellationToken)
        {
            var lockKey = string.Empty;
            var lockValue = string.Empty;

            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    var sessionResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                        ct => _activeSessionStore.GetSessionAsync(sessionId, ct),
                        cancellationToken,
                        onRetry: LogRetry<ChatSession>(tracingOperationName));

                    if (sessionResult.IsFailure)
                        return Result<Unit>.Failure(sessionResult.Error!, sessionResult.ErrorCode);

                    var session = sessionResult.Value!;

                    var chatMessage = new ChatMessage
                    {
                        Role = ChatRole.User,
                        Content = message,
                        Timestamp = DateTime.UtcNow
                    };

                    lockKey = NamingConventionBuilder.LockKeyBuild(session.UserId);

                    var lockResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<string>(
                        ct => _lockManager.AcquireLockAsync(lockKey, ct),
                        cancellationToken,
                        onRetry: LogRetry<string>(tracingOperationName));

                    if (lockResult.IsFailure)
                        return Result<Unit>.Failure(lockResult.Error!, lockResult.ErrorCode);

                    lockValue = lockResult.Value!;
                    session.AddMessage(chatMessage);

                    var saveTask = _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                        ct => _activeSessionStore.SaveSessionAsync(session, ct),
                        cancellationToken,
                        onRetry: LogRetry<Unit>(tracingOperationName));

                    var sendResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                        ct => _chatBackend.SendUserMessageAsync(sessionId, message, ct),
                        cancellationToken,
                        onRetry: LogRetry<Unit>(tracingOperationName));

                    var saveResult = await saveTask;

                    if (sendResult.IsFailure)
                        return sendResult;

                    return Result<Unit>.Success(Unit.Value);
                }
                finally
                {
                    if (!string.IsNullOrEmpty(lockKey) && !string.IsNullOrEmpty(lockValue))
                        await _lockManager.ReleaseLockAsync(lockKey, lockValue);
                }
            }
        }

        public async IAsyncEnumerable<string> StreamReplyAsync(string sessionId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var lockKey = string.Empty;
            var lockValue = string.Empty;
            string accumulatedResponse = string.Empty;

            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    var sessionResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                        ct => _activeSessionStore.GetSessionAsync(sessionId, ct),
                        cancellationToken,
                        onRetry: LogRetry<ChatSession>(tracingOperationName));

                    if (sessionResult.IsFailure)
                        yield break;

                    var session = sessionResult.Value!;

                    lockKey = NamingConventionBuilder.LockKeyBuild(session.UserId);
                    var lockResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<string>(
                        ct => _lockManager.AcquireLockAsync(lockKey, ct),
                        cancellationToken,
                        onRetry: LogRetry<string>(tracingOperationName));

                    if (lockResult.IsFailure)
                        yield break;

                    lockValue = lockResult.Value!;

                    await foreach (var response in _chatBackend.StreamAssistantReplyAsync(sessionId, cancellationToken))
                    {
                        accumulatedResponse += response;
                        yield return response;
                    }

                    var assistantMessage = new ChatMessage
                    {
                        Role = ChatRole.Assistant,
                        Content = accumulatedResponse,
                        Timestamp = DateTime.UtcNow
                    };

                    session.AddMessage(assistantMessage);

                    await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                        ct => _activeSessionStore.SaveSessionAsync(session, ct),
                        cancellationToken,
                        onRetry: LogRetry<Unit>(tracingOperationName));
                }
                finally
                {
                    if (!string.IsNullOrEmpty(lockKey) && !string.IsNullOrEmpty(lockValue))
                        await _lockManager.ReleaseLockAsync(lockKey, lockValue);
                }
            }
        }

        public async Task<Result<Unit>> EndSessionAsync(string sessionId)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var closeResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    ct => _chatBackend.CloseConnectionAsync(sessionId),
                    CancellationToken.None,
                    onRetry: LogRetry<Unit>(tracingOperationName));

                if (closeResult.IsFailure)
                    return closeResult;

                var sessionResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                    _ => _activeSessionStore.GetSessionAsync(sessionId),
                    CancellationToken.None,
                    onRetry: LogRetry<ChatSession>(tracingOperationName));

                if (sessionResult.IsFailure || sessionResult.Value is null)
                    return Result<Unit>.Success(Unit.Value);

                var archiveResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    _ => _archiveSessionStore.PersistSessionAsync(sessionResult.Value!),
                    CancellationToken.None,
                    onRetry: LogRetry<Unit>(tracingOperationName));

                if (archiveResult.IsFailure)
                    return archiveResult;

                var deleteResult = await _retryRunner.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    _ => _activeSessionStore.DeleteSessionAsync(sessionResult.Value!),
                    CancellationToken.None,
                    onRetry: LogRetry<Unit>(tracingOperationName));

                if (deleteResult.IsFailure)
                    return deleteResult;

                return Result<Unit>.Success(Unit.Value);
            }
        }

        private Func<RetryContext<T>, ValueTask> LogRetry<T>(string operationName)
        {
            return ctx =>
            {
                var (source, operation) = CallerInfo.GetCallerClassAndMethod();

                var message =
                    $"Retry #{ctx.Attempt} after {ctx.Delay.TotalMilliseconds}ms in '{operationName}'. " +
                    $"Error: {ctx.Result.Error}";

                _logger.LogWarning(source, operation, message);

                return ValueTask.CompletedTask;
            };
        }
    }
}
