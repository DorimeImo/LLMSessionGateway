using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LLMSessionGateway.Infrastructure.Grpc;
using LLMSessionGateway.Tests.UnitTests.Helpers;
using Moq;
using Observability.Shared.Contracts;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Grpc
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
                _tracingServiceMock.Object);
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
                .Setup(c => c.OpenSessionAsync(It.IsAny<OpenSessionRequest>(), It.IsAny<Metadata>(), null, ct))
                .Returns(CreateAsyncUnaryCall(new Empty()));

            // Act
            var result = await _chatBackend.OpenConnectionAsync(sessionId, userId, ct);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _grpcClientMock.Verify(c => c.OpenSessionAsync(
                It.Is<OpenSessionRequest>(r => r.SessionId == sessionId && r.UserId == userId),
                It.IsAny<Metadata>(), null, ct), Times.Once);
        }

        [Fact]
        public async Task SendUserMessageAsync_Success_ReturnsSuccess()
        {
            // Arrange
            var sessionId = "s";
            var message = "msg";
            var ct = CancellationToken.None;

            _grpcClientMock
                .Setup(c => c.SendMessageAsync(It.IsAny<UserMessageRequest>(), It.IsAny<Metadata>(), null, ct))
                .Returns(CreateAsyncUnaryCall(new Empty()));

            // Act
            var result = await _chatBackend.SendUserMessageAsync(sessionId, message, ct);

            // Assert
            result.IsSuccess.Should().BeTrue();

            _grpcClientMock.Verify(c => c.SendMessageAsync(
                It.Is<UserMessageRequest>(r => r.SessionId == sessionId && r.Message == message),
                It.IsAny<Metadata>(), null, ct), Times.Once);
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
                    if (tokens.Count > 0)
                        tokens.Dequeue();
                });

            var callMock = new AsyncServerStreamingCall<AssistantReplyToken>(
                responseStreamMock.Object,
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { });

            _grpcClientMock
                .Setup(c => c.StreamReply(It.IsAny<StreamReplyRequest>(), It.IsAny<Metadata>(), null, It.IsAny<CancellationToken>()))
                .Returns(callMock);

            // Act
            var results = new List<string>();
            await foreach (var token in _chatBackend.StreamAssistantReplyAsync("s", default))
            {
                results.Add(token);
            }

            // Assert
            results.Should().ContainInOrder("Hello", "world");
        }

        [Fact]
        public async Task CloseConnectionAsync_Success_ReturnsSuccess()
        {
            // Arrange
            _grpcClientMock
                .Setup(c => c.CloseSessionAsync(It.IsAny<CloseSessionRequest>(), It.IsAny<Metadata>(), null, It.IsAny<CancellationToken>()))
                .Returns(CreateAsyncUnaryCall(new Empty()));

            // Act
            var result = await _chatBackend.CloseConnectionAsync("s");

            // Assert
            result.IsSuccess.Should().BeTrue();
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
                .Setup(c => c.OpenSessionAsync(It.IsAny<OpenSessionRequest>(), It.IsAny<Metadata>(), null, It.IsAny<CancellationToken>()))
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
                .Setup(c => c.CloseSessionAsync(It.IsAny<CloseSessionRequest>(), It.IsAny<Metadata>(), null, It.IsAny<CancellationToken>()))
                .Throws(rpcException);

            // Act
            var result = await _chatBackend.CloseConnectionAsync("session-123");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("GRPC_ERROR");

            _loggerMock.VerifyAnyLogging(rpcException);
        }

        [Fact]
        public async Task StreamAssistantReplyAsync_TokensBeforeException_AreReturned_AndNoLoggerCalled()
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
                .Setup(c => c.StreamReply(It.IsAny<StreamReplyRequest>(), It.IsAny<Metadata>(), null, It.IsAny<CancellationToken>()))
                .Returns(callMock);

            // Act
            var results = new List<string>();
            var enumerator = _chatBackend.StreamAssistantReplyAsync("s", default).GetAsyncEnumerator();

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
            results.Should().BeEquivalentTo(new[] { "first", "second" }, options => options.WithStrictOrdering());

            // Optionally verify no error was logged for streaming errors
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
    }
}
