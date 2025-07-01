using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Core.Utilities.Functional;
using StackExchange.Redis;
using System.Text.Json;

namespace LLMSessionGateway.Infrastructure.Redis
{
    public static class RedisErrorHandler
    {
        public static Result<T> Handle<T>(Exception ex, string source, string operation, IStructuredLogger logger)
        {
            switch (ex)
            {
                case RedisTimeoutException redisTimeoutEx:
                    logger.LogWarning(source, operation, "Redis timeout.", redisTimeoutEx);
                    return Result<T>.Failure("Redis timeout", "REDIS_TIMEOUT", isRetryable: true);

                case RedisConnectionException redisConnEx:
                    logger.LogWarning(source, operation, "Redis connection error.", redisConnEx);
                    return Result<T>.Failure("Redis connection error", "REDIS_CONNECTION", isRetryable: true);

                case RedisServerException redisServerEx:
                    logger.LogError(source, operation, "Redis server error.", redisServerEx);
                    return Result<T>.Failure("Redis server error", "REDIS_SERVER_ERROR", isRetryable: false);

                case RedisException redisEx:
                    logger.LogError(source, operation, $"Redis error of type {redisEx.GetType().Name}.", redisEx);
                    return Result<T>.Failure("Redis error", "REDIS_ERROR", isRetryable: false);

                case OperationCanceledException cancelEx:
                    logger.LogWarning(source, operation, "Operation was canceled.", cancelEx);
                    return Result<T>.Failure($"{operation} was cancelled", "CANCELLED", isRetryable: false);

                case JsonException jsonEx:
                    logger.LogError(source, operation, "JSON error.", jsonEx);
                    return Result<T>.Failure("Serialization or deserialization error", "JSON_ERROR", isRetryable: false);

                default:
                    throw ex;
            }
        }
    }
}
