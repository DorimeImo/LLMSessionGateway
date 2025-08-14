using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage;
using Moq;
using Observability.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Controller.Helpers
{
    public static class IntegrationTestHelpers
    {
        public static Mock<IStructuredLogger> CreateLoggerMock()
        {
            var mock = new Mock<IStructuredLogger>();
            mock.SetupAllProperties();
            mock.SetupGet(x => x.Current).Returns(new LogContext
            {
                SessionId = null,
                TraceId = "test-trace"
            });
            return mock;
        }

        public static Mock<ITracingService> CreateTracingMock()
        {
            var mock = new Mock<ITracingService>();
            mock.Setup(t => t.StartActivity(It.IsAny<string>()))
                .Returns(new DummyActivity());
            return mock;
        }

        public static async IAsyncEnumerable<string> FakeStream()
        {
            yield return "First chunk";
            yield return "Second chunk";
            await Task.CompletedTask;
        }

        private class DummyActivity : IDisposable
        {
            public void Dispose() { }
        }

        public static async Task AssertResponseSuccess(HttpResponseMessage response, string operation)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{operation} failed: {response.StatusCode}, {content}");
        }

        public static void ConfigureActiveSessionStore(Mock<IActiveSessionStore> mock)
        {
            mock.Setup(m => m.SaveSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            mock.Setup(m => m.GetSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(new ChatSession { SessionId = "abc", UserId = "user" }));

            mock.Setup(m => m.DeleteSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            mock.Setup(m => m.GetActiveSessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Failure("No active session.", errorCode: "NO_ACTIVE_SESSION"));
        }

        public static void ConfigureArchiveSessionStoreMock(Mock<IArchiveSessionStore> mock)
        {
            mock.Setup(m => m.PersistSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));
        }

        public static void ConfigureChatBackendMock(Mock<IChatBackend> mock)
        {
            mock.Setup(m => m.OpenConnectionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            mock.Setup(m => m.SendUserMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            mock.Setup(m => m.StreamAssistantReplyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(FakeStream());

            mock.Setup(m => m.CloseConnectionAsync(It.IsAny<string>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));
        }

        public static void ConfigureRetryPolicyMock(Mock<IRetryPolicyRunner> mock)
        {
            // For Result<Unit>
            mock.Setup(m => m.ExecuteAsyncWithRetryAndFinalyze(
                It.IsAny<Func<CancellationToken, Task<Result<Unit>>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<RetryContext<Unit>, ValueTask>>()
            ))
            .Returns((Func<CancellationToken, Task<Result<Unit>>> func,
                      CancellationToken ct,
                      int retryCount,
                      int delayMs,
                      Func<RetryContext<Unit>, ValueTask> onRetry)
                => func(ct));

            // For Result<string>
            mock.Setup(m => m.ExecuteAsyncWithRetryAndFinalyze(
                It.IsAny<Func<CancellationToken, Task<Result<string>>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<RetryContext<string>, ValueTask>>()
            ))
            .Returns((Func<CancellationToken, Task<Result<string>>> func,
                      CancellationToken ct,
                      int retryCount,
                      int delayMs,
                      Func<RetryContext<string>, ValueTask> onRetry)
                => func(ct));

            // For Result<ChatSession>
            mock.Setup(m => m.ExecuteAsyncWithRetryAndFinalyze(
                It.IsAny<Func<CancellationToken, Task<Result<ChatSession>>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<Func<RetryContext<ChatSession>, ValueTask>>()
            ))
            .Returns((Func<CancellationToken, Task<Result<ChatSession>>> func,
                      CancellationToken ct,
                      int retryCount,
                      int delayMs,
                      Func<RetryContext<ChatSession>, ValueTask> onRetry)
                => func(ct));
        }

        public static void ConfigureLockManagerMock(Mock<IDistributedLockManager> mock)
        {
            mock.Setup(m => m.RunWithLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<Result<string>>>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns<string, Func<CancellationToken, Task<Result<string>>>, CancellationToken>(
                    async (key, action, ct) => await action(ct)
                );

            mock.Setup(m => m.RunWithLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<Result<ChatSession>>>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns<string, Func<CancellationToken, Task<Result<ChatSession>>>, CancellationToken>(
                    async (key, action, ct) => await action(ct)
                );

            mock.Setup(m => m.RunWithLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<Result<Unit>>>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns<string, Func<CancellationToken, Task<Result<Unit>>>, CancellationToken>(
                    async (key, action, ct) => await action(ct)
                );
        }
    }
}
