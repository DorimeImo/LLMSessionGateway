using FluentAssertions;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Tests.UnitTests.Application.Helpers;
using Moq;
using Observability.Shared.Contracts;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Application
{
    public class ChatSessionUpdaterTests
    {
        private readonly Mock<IActiveSessionStore> _activeStoreMock = new();
        private readonly Mock<IChatSessionService> _sessionServiceMock = new();
        private readonly Mock<IRetryPolicyRunner> _retryRunnerMock = new();
        private readonly Mock<ITracingService> _tracingMock = new();
        private readonly Mock<IStructuredLogger> _loggerMock = new();

        private readonly ChatSessionUpdater _updater;

        public ChatSessionUpdaterTests()
        {
            _updater = new ChatSessionUpdater(
                _activeStoreMock.Object,
                _sessionServiceMock.Object,
                _retryRunnerMock.Object,
                _tracingMock.Object,
                _loggerMock.Object
            );
            RetryMockExtensions.SetupRetryRunnerMock(_retryRunnerMock);
        }

        [Fact]
        public async Task AddMessageAsync_ShouldAppendMessage_AndSaveSession()
        {
            // Arrange
            var userId = "user123";
            var sessionId = "session-123";
            var role = ChatRole.User;
            var content = "hello";
            var session = new ChatSession { SessionId = sessionId, UserId = userId };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _activeStoreMock
                .Setup(s => s.SaveSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            // Act
            var result = await _updater.AddMessageAsync(sessionId, role, content);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _sessionServiceMock.Verify(s => s.AddMessage(session, role, content), Times.Once);
            _activeStoreMock.Verify(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
            _activeStoreMock.Verify(s => s.SaveSessionAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddMessageAsync_ShouldReturnFailure_WhenSessionNotFound()
        {
            // Arrange
            var sessionId = "unknown-session";

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Failure("not found"));

            // Act
            var result = await _updater.AddMessageAsync(sessionId, ChatRole.User, "msg");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("session_not_found");
            _sessionServiceMock.Verify(s => s.AddMessage(It.IsAny<ChatSession>(), It.IsAny<ChatRole>(), It.IsAny<string>()), Times.Never);
            _activeStoreMock.Verify(s => s.SaveSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AddMessageAsync_ShouldReturnFailure_WhenSaveFails()
        {
            // Arrange
            var userId = "user123";
            var sessionId = "session-123";
            var session = new ChatSession { SessionId = sessionId, UserId = userId };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _activeStoreMock
                .Setup(s => s.SaveSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure("save failed"));

            // Act
            var result = await _updater.AddMessageAsync(sessionId, ChatRole.Assistant, "response");

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("save failed");
            _sessionServiceMock.Verify(s => s.AddMessage(session, ChatRole.Assistant, "response"), Times.Once);
        }
    }

}
