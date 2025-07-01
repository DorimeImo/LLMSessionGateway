using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Contracts.Resilience
{
    public interface IRetryPolicyRunner
    {
        Task<Result<T>> ExecuteAsyncWithRetryAndFinalyze<T>(
            Func<CancellationToken, Task<Result<T>>> action,
            CancellationToken ct = default,
            int retryCount = 3,
            int delayMilliseconds = 200,
            Func<RetryContext<T>, ValueTask>? onRetry = null
        );
    }
}
