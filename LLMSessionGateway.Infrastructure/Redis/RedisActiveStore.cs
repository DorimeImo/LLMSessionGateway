using LLMSessionGateway.Application.Contracts.KeyGeneration;
using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Application.Contracts.Observability;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using StackExchange.Redis;
using System.Text.Json;

namespace LLMSessionGateway.Infrastructure.Redis
{
    //TODO: перегрузку Redis
    public class RedisActiveStore : IActiveSessionStore
    {
        private readonly IDatabase _redisDb;
        private readonly TimeSpan _sessionTtl;

        private static readonly LuaScript ConditionalDeleteScript = LuaScript.Prepare(@"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end");

        private readonly IStructuredLogger _logger;
        private readonly ITracingService _tracingService;

        public RedisActiveStore(IConnectionMultiplexer redis, TimeSpan sessionTtl, IStructuredLogger logger, ITracingService tracingService)
        {
            _redisDb = redis.GetDatabase();
            _sessionTtl = sessionTtl;
            _logger = logger;
            _tracingService = tracingService;
        }

        public async Task<Result<string>> GetActiveSessionIdAsync(string userId, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var value = await _redisDb.StringGetAsync(NamingConventionBuilder.UserActiveKeyBuild(userId));
                    return value.HasValue
                        ? Result<string>.Success(value.ToString())
                        : Result<string>.Failure("No active session found", errorCode: "SESSION_NOT_FOUND", isRetryable: false);
                }
                catch (Exception ex)
                {
                    return RedisErrorHandler.Handle<string>(ex, source, operation, _logger);
                }
            }   
        }

        public async Task<Result<ChatSession>> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var json = await _redisDb.StringGetAsync(NamingConventionBuilder.SessionKeyBuild(sessionId));
                    if (!json.HasValue)
                    {
                        return Result<ChatSession>.Failure("Session not found", errorCode: "SESSION_NOT_FOUND", isRetryable: false);
                    }

                    var session = JsonSerializer.Deserialize<ChatSession>(json!)!;
                    return Result<ChatSession>.Success(session);
                }
                catch (Exception ex)
                {
                    return RedisErrorHandler.Handle<ChatSession>(ex, source, operation, _logger);
                }
            }  
        }

        public async Task<Result<Unit>> SaveSessionAsync(ChatSession session, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var json = JsonSerializer.Serialize(session);

                    var tran = _redisDb.CreateTransaction();

                    _ = tran.StringSetAsync(NamingConventionBuilder.SessionKeyBuild(session.SessionId), json, _sessionTtl);
                    _ = tran.StringSetAsync(NamingConventionBuilder.UserActiveKeyBuild(session.UserId), session.SessionId, _sessionTtl);

                    bool committed = await tran.ExecuteAsync();

                    return committed
                        ? Result<Unit>.Success(Unit.Value)
                        : Result<Unit>.Failure("Redis transaction failed to save session", errorCode: "TRANSACTION_FAILED", isRetryable: true);
                }
                catch (Exception ex)
                {
                    return RedisErrorHandler.Handle<Unit>(ex, source, operation, _logger);
                }
            }
        }

        public async Task<Result<Unit>> DeleteSessionAsync(ChatSession session, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await _redisDb.KeyDeleteAsync(NamingConventionBuilder.SessionKeyBuild(session.SessionId));

                    var userActiveKey = NamingConventionBuilder.UserActiveKeyBuild(session.UserId);
                    var expectedSessionId = session.SessionId;

                    await _redisDb.ScriptEvaluateAsync(ConditionalDeleteScript, new
                    {
                        KEYS = new[] { userActiveKey },
                        ARGV = new[] { expectedSessionId }
                    });

                    return Result<Unit>.Success(Unit.Value);
                }
                catch (Exception ex)
                {
                    return RedisErrorHandler.Handle<Unit>(ex, source, operation, _logger);
                }
            }   
        }

        public Task<Result<Unit>> AppendMessageAsync(ChatSession session, ChatMessage message, CancellationToken ct)
        {
            return Task.FromResult(Result<Unit>.Failure("AppendMessageAsync is not supported in Redis", errorCode: "NOT_SUPPORTED"));
        }
    }
}
