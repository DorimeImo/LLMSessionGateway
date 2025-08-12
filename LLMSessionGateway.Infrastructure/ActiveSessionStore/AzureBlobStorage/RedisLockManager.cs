using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using StackExchange.Redis;

namespace LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage
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

        public async Task<Result<T>> RunWithLockAsync<T>(
        string lockKey,
        Func<CancellationToken, Task<Result<T>>> action,
        CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperation = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperation))
            {
                var lockValue = Guid.NewGuid().ToString();

                try
                {
                    ct.ThrowIfCancellationRequested();

                    var acquired = await _redisDb.StringSetAsync(lockKey, lockValue, _lockTtl, When.NotExists);
                    if (!acquired)
                    {
                        return Result<T>.Failure(
                            "Failed to acquire Redis lock",
                            errorCode: "REDIS_LOCK_FAILED",
                            isRetryable: true);
                    }

                    return await action(ct);
                }
                catch (Exception ex)
                {
                    return RedisErrorHandler.Handle<T>(ex, source, operation, _logger);
                }
                finally
                {
                    try
                    {
                        await _redisDb.ScriptEvaluateAsync(UnlockScript, new
                        {
                            KEYS = new[] { lockKey },
                            ARGV = new[] { lockValue }
                        });
                    }
                    catch (Exception releaseEx)
                    {
                        _logger.LogError(source, operation, $"Failed to release lock {lockKey}. Error: {releaseEx.Message}");
                    }
                }
            }
        }
    }
}
