using LLMSessionGateway.Application.Contracts.KeyGeneration;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using StackExchange.Redis;

namespace LLMSessionGateway.Infrastructure.Redis
{
    public class RedisLockManager : IDistributedLockManager
    {
        private readonly IDatabase _redisDb;
        private static readonly LuaScript UnlockScript = LuaScript.Prepare(@"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end");

        private readonly IStructuredLogger _logger;
        private readonly ITracingService _tracingService;
        private readonly TimeSpan _lockTtl;

        public RedisLockManager(IConnectionMultiplexer redis, IStructuredLogger logger, ITracingService tracingService, TimeSpan lockTtl)
        {
            _redisDb = redis.GetDatabase();
            _logger = logger;
            _tracingService = tracingService;
            _lockTtl = lockTtl;
        }

        public async Task<Result<string>> AcquireLockAsync(string lockKey, CancellationToken ct)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    var lockValue = Guid.NewGuid().ToString();
                    bool acquired = await _redisDb.StringSetAsync(lockKey, lockValue, _lockTtl, When.NotExists);

                    if (!acquired)
                    {
                        return Result<string>.Failure("Failed to acquire Redis lock", errorCode: "REDIS_LOCK_FAILED", isRetryable: true);
                    }

                    return Result<string>.Success(lockValue);
                }
                catch (Exception ex)
                {
                    return RedisErrorHandler.Handle<string>(ex, source, operation, _logger);
                }
            }  
        }

        public async Task<Result<Unit>> ReleaseLockAsync(string lockKey, string lockValue)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    await _redisDb.ScriptEvaluateAsync(UnlockScript, new { KEYS = new[] { lockKey }, ARGV = new[] { lockValue } });
                    return Result<Unit>.Success(Unit.Value);
                }
                catch (Exception ex)
                {
                    return RedisErrorHandler.Handle<Unit>(ex, source, operation, _logger);
                }
            } 
        }
    }
}
