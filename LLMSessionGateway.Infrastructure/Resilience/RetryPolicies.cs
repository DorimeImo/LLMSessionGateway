using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Core.Utilities.Functional;
using Polly;
using Polly.Retry;

namespace LLMSessionGateway.Infrastructure.Resilience
{
    public class RetryPolicies : IRetryPolicyRunner
    {
        public async Task<Result<T>> ExecuteAsyncWithRetryAndFinalyze<T>(
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken ct = default,
            int retryCount = 3,
            int delayMilliseconds = 200,
            Func<RetryContext<T>, ValueTask>? onRetry = null)
        {
            var options = new RetryStrategyOptions<Result<T>>
            {
                ShouldHandle = new PredicateBuilder<Result<T>>()
                    .HandleResult(r => r.IsFailure && r.IsRetryable),

                DelayGenerator = args =>
                    ValueTask.FromResult<TimeSpan?>(TimeSpan.FromMilliseconds(delayMilliseconds * args.AttemptNumber)),

                MaxRetryAttempts = retryCount,

                OnRetry = onRetry == null
                    ? _ => ValueTask.CompletedTask
                    : args =>
                    {
                        var ctx = new RetryContext<T>(
                            Attempt: args.AttemptNumber,
                            Delay: args.RetryDelay,
                            Result: args.Outcome.Result!
                        );
                        return onRetry(ctx);
                    }
            };

            var pipeline = new ResiliencePipelineBuilder<Result<T>>()
                .AddRetry(options)
                .Build();

            var result = await pipeline.ExecuteAsync<Result<T>, Func<CancellationToken, Task<Result<T>>>>(
                callback: (context, state) => new ValueTask<Result<T>>(state(context.CancellationToken)),
                context: ResilienceContextPool.Shared.Get(),
                state: action
            );

            if (result.IsFailure && result.IsRetryable)
                return Result<T>.Failure(result.Error!, result.ErrorCode);

            return result;
        }
    }
}
