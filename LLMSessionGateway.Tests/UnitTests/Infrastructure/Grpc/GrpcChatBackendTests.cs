using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Tests.UnitTests.Infrastructure.Helpers;
using Moq;
using Observability.Shared.Contracts;
using System.Threading;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Infrastructure.Grpc
{
    public class GrpcChatBackendTests
    {
        private readonly Mock<ChatService.ChatServiceClient> _grpcClientMock = new();
        private readonly Mock<IStructuredLogger> _loggerMock = new();
        private readonly Mock<ITracingService> _tracingServiceMock = new();
        private readonly GrpcChatBackend _chatBackend;

        public GrpcChatBackendTests()
        {
            _chatBackend = new GrpcChatBackend(
                _grpcClientMock.Object,
                _loggerMock.Object,
                _tracingServiceMock.Object,
                CreateTimeoutsOptions());
        }

        // -----------------------------------------
        // ✅ Core success and expected flow tests
        // -----------------------------------------

        [Fact]
        public async Task OpenConnectionAsync_Success_ReturnsSuccessResult()
        {
            // Arrange
            var sessionId = "session-1";
            var userId = "user-1";
            var ct = CancellationToken.None;

            _grpcClientMock
                .Setup(c => c.OpenSessionAsync(
                    It.IsAny<OpenSessionRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new Empty()));

            // Act
            var result = await _chatBackend.OpenConnectionAsync(sessionId, userId, ct);

            // Assert
            result.IsSuccess.Should().BeTrue();

            _grpcClientMock.Verify(c => c.OpenSessionAsync(
                It.Is<OpenSessionRequest>(r => r.SessionId == sessionId && r.UserId == userId),
                It.Is<Metadata>(m =>
                    m.Any(e => e.Key == "x-session-id" && e.Value == sessionId) &&
                    m.Any(e => e.Key == "x-user-id" && e.Value == userId)),
                It.Is<DateTime?>(d => d.HasValue),
                ct), Times.Once);
        }

        [Fact]
        public async Task SendUserMessageAsync_Success_ReturnsSuccess()
        {
            // Arrange
            var sessionId = "s";
            var message = "msg";
            var messageId = "pm1";
            var ct = CancellationToken.None;

            _grpcClientMock
                .Setup(c => c.SendMessageAsync(
                    It.IsAny<UserMessageRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new Empty()));

            // Act
            var result = await _chatBackend.SendUserMessageAsync(sessionId, message, messageId, ct);

            // Assert
            result.IsSuccess.Should().BeTrue();

            _grpcClientMock.Verify(c => c.SendMessageAsync(
                It.Is<UserMessageRequest>(r =>
                    r.SessionId == sessionId &&
                    r.Message == message &&
                    r.MessageId == messageId),
                It.Is<Metadata>(m =>
                    m.Any(e => e.Key == "x-session-id" && e.Value == sessionId) &&
                    m.Any(e => e.Key == "x-message-id" && e.Value == messageId)),
                It.Is<DateTime?>(d => d.HasValue),
                ct), Times.Once);
        }

        [Fact]
        public async Task StreamAssistantReplyAsync_StreamsData()
        {
            // Arrange
            var responseStreamMock = new Mock<IAsyncStreamReader<AssistantReplyToken>>();
            var tokens = new Queue<string>(new[] { "Hello", "world" });

            responseStreamMock
                .SetupSequence(r => r.MoveNext(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ReturnsAsync(false);

            responseStreamMock
                .SetupGet(r => r.Current)
                .Returns(() => new AssistantReplyToken { Token = tokens.Peek() })
                .Callback(() =>
                {
                    if (tokens.Count > 0) tokens.Dequeue();
                });

            var callMock = new AsyncServerStreamingCall<AssistantReplyToken>(
                responseStreamMock.Object,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

            _grpcClientMock
                .Setup(c => c.StreamReply(
                    It.IsAny<StreamReplyRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(callMock);

            // Act
            var results = new List<string>();
            await foreach (var token in _chatBackend.StreamAssistantReplyAsync("s", "pm1", default))
            {
                results.Add(token);
            }

            // Assert
            results.Should().ContainInOrder("Hello", "world");

            // Optional: verify headers sent on stream setup
            _grpcClientMock.Verify(c => c.StreamReply(
                It.Is<StreamReplyRequest>(r => r.SessionId == "s" && r.MessageId == "pm1"),
                It.Is<Metadata>(m =>
                    m.Any(e => e.Key == "x-session-id" && e.Value == "s") &&
                    m.Any(e => e.Key == "x-message-id" && e.Value == "pm1")),
                It.Is<DateTime?>(d => d.HasValue),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CloseConnectionAsync_Success_ReturnsSuccess()
        {
            // Arrange
            _grpcClientMock
                .Setup(c => c.CloseSessionAsync(
                    It.IsAny<CloseSessionRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new Empty()));

            // Act
            var result = await _chatBackend.CloseConnectionAsync("s");

            // Assert
            result.IsSuccess.Should().BeTrue();
            _grpcClientMock.Verify(c => c.CloseSessionAsync(
                It.IsAny<CloseSessionRequest>(),
                It.IsAny<Metadata>(),
                It.Is<DateTime?>(d => d.HasValue),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // -----------------------------------------
        // ❌ Exception handling and logging verification
        // -----------------------------------------

        [Fact]
        public async Task OpenConnectionAsync_ThrowsException_ReturnsFailureResult_AndLogsWarning()
        {
            // Arrange
            var rpcException = new RpcException(new Status(StatusCode.Unavailable, "Service down"));

            _grpcClientMock
                .Setup(c => c.OpenSessionAsync(
                    It.IsAny<OpenSessionRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Throws(rpcException);

            // Act
            var result = await _chatBackend.OpenConnectionAsync("s", "u", default);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("GRPC_ERROR");
            _loggerMock.VerifyAnyLogging(rpcException);
        }

        [Fact]
        public async Task CloseConnectionAsync_ThrowsException_ReturnsFailure_AndLogsWarning()
        {
            // Arrange
            var rpcException = new RpcException(new Status(StatusCode.Unavailable, "Service unavailable"));

            _grpcClientMock
                .Setup(c => c.CloseSessionAsync(
                    It.IsAny<CloseSessionRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Throws(rpcException);

            // Act
            var result = await _chatBackend.CloseConnectionAsync("session-123");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("GRPC_ERROR");
            _loggerMock.VerifyAnyLogging(rpcException);
        }

        [Fact]
        public async Task StreamAssistantReplyAsync_TokensBeforeException_AreReturned_ThenEnumeratorThrows()
        {
            // Arrange
            var responseStreamMock = new Mock<IAsyncStreamReader<AssistantReplyToken>>();
            var tokens = new Queue<string>(new[] { "first", "second" });

            responseStreamMock
                .SetupSequence(r => r.MoveNext(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true)
                .ReturnsAsync(true)
                .ThrowsAsync(new RpcException(new Status(StatusCode.Internal, "Stream error")));

            responseStreamMock
                .SetupGet(r => r.Current)
                .Returns(() => new AssistantReplyToken { Token = tokens.Peek() })
                .Callback(() => tokens.Dequeue());

            var callMock = new AsyncServerStreamingCall<AssistantReplyToken>(
                responseStreamMock.Object,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

            _grpcClientMock
                .Setup(c => c.StreamReply(
                    It.IsAny<StreamReplyRequest>(),
                    It.IsAny<Metadata>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(callMock);

            // Act
            var results = new List<string>();
            var enumerator = _chatBackend.StreamAssistantReplyAsync("s", "pm1", default).GetAsyncEnumerator();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    results.Add(enumerator.Current);
                }
            }
            catch (RpcException ex)
            {
                ex.StatusCode.Should().Be(StatusCode.Internal);
            }

            // Assert tokens collected before error
            results.Should().BeEquivalentTo(new[] { "first", "second" }, o => o.WithStrictOrdering());

            // For StatusCode.Internal we rethrow (no warning logged by StreamSafe)
            _loggerMock.VerifyNoOtherCalls();
        }

        // -----------------------------------------
        // 🛠️ Helpers
        // -----------------------------------------

        private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
        {
            return new AsyncUnaryCall<T>(
                Task.FromResult(response),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });
        }

        private GrpcTimeoutsConfigs CreateTimeoutsOptions()
        {
            // New config class: seconds instead of TimeSpan properties
            return new GrpcTimeoutsConfigs
            {
                OpenSeconds = 5,
                SendSeconds = 10,
                StreamSetupSeconds = 10,
                CloseSeconds = 5
            };
        }
    }
}
