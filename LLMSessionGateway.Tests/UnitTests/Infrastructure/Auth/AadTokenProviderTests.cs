using Azure.Core;
using Azure.Identity;
using FluentAssertions;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Infrastructure.Auth.AzureAD;
using Moq;
using Observability.Shared.Contracts;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Infrastructure.Auth
{
    public class AadTokenProviderTests
    {
        private const string Scope = "api://chat-backend/.default";

        private readonly Mock<IStructuredLogger> _logger = new();
        private readonly Mock<ITracingService> _tracing = new();

        public AadTokenProviderTests()
        {
            _tracing.Setup(t => t.StartActivity(It.IsAny<string>()))
                    .Returns(Mock.Of<IDisposable>());
        }

        [Fact]
        public async Task GetTokenAsync_FirstCall_FetchesAndCaches()
        {
            // Arrange
            var cred = new StubTokenCredential((_, _) =>
                new ValueTask<AccessToken>(new AccessToken("tok-1", DateTimeOffset.UtcNow.AddMinutes(60))));

            var sut = new AadTokenProvider(_logger.Object, _tracing.Object, cred);

            // Act
            var res = await sut.GetTokenAsync(Scope);

            // Assert
            res.IsSuccess.Should().BeTrue();
            res.Value.Should().Be("tok-1");
            cred.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task GetTokenAsync_SecondCall_UsesCache_NoFetch()
        {
            // Arrange
            var cred = new StubTokenCredential((_, _) =>
                new ValueTask<AccessToken>(new AccessToken("tok-1", DateTimeOffset.UtcNow.AddMinutes(60))));

            var sut = new AadTokenProvider(_logger.Object, _tracing.Object, cred);

            // Act
            var first = await sut.GetTokenAsync(Scope);
            var second = await sut.GetTokenAsync(Scope);

            // Assert
            first.IsSuccess.Should().BeTrue();
            second.IsSuccess.Should().BeTrue();
            first.Value.Should().Be("tok-1");
            second.Value.Should().Be("tok-1");
            cred.CallCount.Should().Be(1); // cached on second call
        }

        [Fact]
        public async Task GetTokenAsync_EarlyRefresh_RefreshesWhenNearExpiry()
        {
            // Arrange: first token expires in ~1 minute (less than EarlyRefresh=5m), so second call should refresh
            var call = 0;
            var cred = new StubTokenCredential((_, _) =>
            {
                call++;
                var expires = (call == 1)
                    ? DateTimeOffset.UtcNow.AddMinutes(1)  // near expiry -> triggers refresh on next call
                    : DateTimeOffset.UtcNow.AddMinutes(60);
                var token = new AccessToken($"tok-{call}", expires);
                return new ValueTask<AccessToken>(token);
            });

            var sut = new AadTokenProvider(_logger.Object, _tracing.Object, cred);

            // Act
            var first = await sut.GetTokenAsync(Scope);
            var second = await sut.GetTokenAsync(Scope);

            // Assert
            first.IsSuccess.Should().BeTrue();
            second.IsSuccess.Should().BeTrue();
            first.Value.Should().Be("tok-1");
            second.Value.Should().Be("tok-2");
            cred.CallCount.Should().Be(2);
        }

        [Fact]
        public async Task GetTokenAsync_ConcurrentSameScope_SingleFetch()
        {
            // Arrange: block the credential until multiple callers are waiting
            var tcs = new TaskCompletionSource<AccessToken>(TaskCreationOptions.RunContinuationsAsynchronously);

            var cred = new StubTokenCredential((_, _) =>
            {
                Interlocked.Increment(ref StubTokenCredential.BlockedWaiters);
                return new ValueTask<AccessToken>(tcs.Task);
            });

            var sut = new AadTokenProvider(_logger.Object, _tracing.Object, cred);

            // Act: fire multiple concurrent requests for the same scope
            var tasks = new List<Task<Result<string>>>();
            for (int i = 0; i < 8; i++)
                tasks.Add(sut.GetTokenAsync(Scope));

            // Release the single underlying fetch
            tcs.SetResult(new AccessToken("tok-concurrent", DateTimeOffset.UtcNow.AddMinutes(60)));

            var results = await Task.WhenAll(tasks);

            // Assert: all succeeded, all same token, credential called only once
            results.Should().OnlyContain(r => r.IsSuccess && r.Value == "tok-concurrent");
            cred.CallCount.Should().Be(1);
        }

        [Fact]
        public async Task GetTokenAsync_Cancellation_ReturnsFailure()
        {
            // Arrange: token source already cancelled
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var cred = new StubTokenCredential((_, _) =>
                new ValueTask<AccessToken>(new AccessToken("tok", DateTimeOffset.UtcNow.AddMinutes(60))));

            var sut = new AadTokenProvider(_logger.Object, _tracing.Object, cred);

            // Act
            var res = await sut.GetTokenAsync(Scope, cts.Token);

            // Assert
            res.IsFailure.Should().BeTrue();
            res.ErrorCode.Should().Be("CANCELLED"); // per TokenErrorHandler
            cred.CallCount.Should().Be(0);
        }

        [Fact]
        public async Task GetTokenAsync_CredentialThrows_ReturnsFailure()
        {
            // Arrange
            var cred = new StubTokenCredential((_, _) =>
                throw new AuthenticationFailedException("boom"));

            var sut = new AadTokenProvider(_logger.Object, _tracing.Object, cred);

            // Act
            var res = await sut.GetTokenAsync(Scope);

            // Assert
            res.IsFailure.Should().BeTrue();
            // Depending on your TokenErrorHandler mapping:
            res.ErrorCode.Should().BeOneOf("AUTH_FAILED", "AUTHORITY_ERROR", "CREDENTIAL_UNAVAILABLE", "CANCELLED");
            cred.CallCount.Should().Be(1);
        }

        // ---------- helpers ----------

        private sealed class StubTokenCredential : TokenCredential
        {
            private readonly Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> _onGet;
            private int _callCount;

            public StubTokenCredential(Func<TokenRequestContext, CancellationToken, ValueTask<AccessToken>> onGet)
            {
                _onGet = onGet;
            }

            public int CallCount => _callCount;
            public static int BlockedWaiters;

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
                => throw new NotSupportedException("Synchronous GetToken is not used in tests.");

            public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _callCount);
                return await _onGet(requestContext, cancellationToken);
            }
        }
    }
}
