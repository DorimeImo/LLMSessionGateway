using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.UnitTests.Application.Helpers
{
    public static class RetryMockExtensions
    {
        public static void SetupRetryRunnerMock(Mock<IRetryPolicyRunner> retryRunnerMock)
        {
            retryRunnerMock
                .Setup(m => m.ExecuteAsyncWithRetryAndFinalyze<string>(
                    It.IsAny<Func<CancellationToken, Task<Result<string>>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<RetryContext<string>, ValueTask>>()))
                .Returns<Func<CancellationToken, Task<Result<string>>>, CancellationToken, int, int, Func<RetryContext<string>, ValueTask>>(
                    async (func, ct, _, _, _) => await func(ct));

            retryRunnerMock
                .Setup(m => m.ExecuteAsyncWithRetryAndFinalyze<ChatSession>(
                    It.IsAny<Func<CancellationToken, Task<Result<ChatSession>>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<RetryContext<ChatSession>, ValueTask>>()))
                .Returns<Func<CancellationToken, Task<Result<ChatSession>>>, CancellationToken, int, int, Func<RetryContext<ChatSession>, ValueTask>>(
                    async (func, ct, _, _, _) => await func(ct));

            retryRunnerMock
                .Setup(m => m.ExecuteAsyncWithRetryAndFinalyze<Unit>(
                    It.IsAny<Func<CancellationToken, Task<Result<Unit>>>>(),
                    It.IsAny<CancellationToken>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<Func<RetryContext<Unit>, ValueTask>>()))
                .Returns<Func<CancellationToken, Task<Result<Unit>>>, CancellationToken, int, int, Func<RetryContext<Unit>, ValueTask>>(
                    async (func, ct, _, _, _) => await func(ct));
        }
    }
}
