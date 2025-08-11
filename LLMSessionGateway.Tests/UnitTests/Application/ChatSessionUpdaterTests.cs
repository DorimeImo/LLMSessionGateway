using FluentAssertions;
using LLMSessionGateway.Application.Contracts.Commands;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Tests.UnitTests.Application.Helpers;
using Moq;
using Observability.Shared.Contracts;
using System.ComponentModel.Design;
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
            var command = CreateCommand();
            var role = ChatRole.User;
            var session = new ChatSession { SessionId = command.SessionId, UserId = userId };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(command.SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _sessionServiceMock
                .Setup(s => s.AddMessageIfAbsent(session, It.Is<ChatMessage>(m =>
                    m.MessageId == command.MessageId &&
                    m.Role == ChatRole.User &&
                    m.Content == command.Message)))
                .Returns(true);

            _activeStoreMock
                .Setup(s => s.SaveSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            // Act
            var result = await _updater.AddMessageAsync(command, role);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _sessionServiceMock.VerifyAll();
            _activeStoreMock.Verify(s => s.GetSessionAsync(command.SessionId, It.IsAny<CancellationToken>()), Times.Once);
            _activeStoreMock.Verify(s => s.SaveSessionAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddMessageAsync_ShouldReturnFailure_WhenSessionNotFound()
        {
            // Arrange
            var command = CreateCommand();

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(command.SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Failure("not found"));

            // Act
            var result = await _updater.AddMessageAsync(command, ChatRole.User);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("session_not_found");
            _sessionServiceMock.Verify(s => s.AddMessageIfAbsent(It.IsAny<ChatSession>(), It.IsAny<ChatMessage>()), Times.Never);
            _activeStoreMock.Verify(s => s.SaveSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AddMessageAsync_ReturnsFailure_WhenDuplicateMessage()
        {
            // Arrange
            var cmd = CreateCommand();
            var session = new ChatSession { SessionId = cmd.SessionId, UserId = "u1" };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(cmd.SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _sessionServiceMock
                .Setup(s => s.AddMessageIfAbsent(session, It.IsAny<ChatMessage>()))
                .Returns(false);

            // Act
            var result = await _updater.AddMessageAsync(cmd, ChatRole.User);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("duplicate_message"); 
            _activeStoreMock.Verify(s => s.SaveSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task AddMessageAsync_ShouldReturnFailure_WhenSaveFails()
        {
            // Arrange
            var userId = "u1";
            var command = CreateCommand();
            var session = new ChatSession { SessionId = command.SessionId, UserId = userId };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(command.SessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _sessionServiceMock
                .Setup(s => s.AddMessageIfAbsent(session, It.IsAny<ChatMessage>()))
                .Returns(true);

            _activeStoreMock
                .Setup(s => s.SaveSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure("save failed"));

            // Act
            var result = await _updater.AddMessageAsync(command, ChatRole.Assistant);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("save failed");
            _sessionServiceMock.Verify(s => s.AddMessageIfAbsent(session, It.IsAny<ChatMessage>()), Times.Once);
        }

        private SendMessageCommand CreateCommand()
        {
            return new SendMessageCommand()
            {
                SessionId = "s1",
                MessageId = "m1",
                Message = "message"
            };
        }
    }
}
