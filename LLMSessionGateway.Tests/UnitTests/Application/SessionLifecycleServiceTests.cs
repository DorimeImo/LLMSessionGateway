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
    public class SessionLifecycleServiceTests
    {
        private readonly Mock<IActiveSessionStore> _activeStoreMock = new();
        private readonly Mock<IArchiveSessionStore> _archiveStoreMock = new();
        private readonly Mock<IRetryPolicyRunner> _retryRunnerMock = new();
        private readonly Mock<ITracingService> _tracingMock = new();
        private readonly Mock<IStructuredLogger> _loggerMock = new();

        private readonly SessionLifecycleService _service;

        public SessionLifecycleServiceTests()
        {
            _service = new SessionLifecycleService(
                _activeStoreMock.Object,
                _archiveStoreMock.Object,
                _retryRunnerMock.Object,
                _tracingMock.Object,
                _loggerMock.Object
            );
            RetryMockExtensions.SetupRetryRunnerMock(_retryRunnerMock);
        }

        [Fact]
        public async Task StartSessionAsync_ShouldReturnExistingSession_IfAlreadyExists()
        {
            // Arrange
            var userId = "user123";
            var sessionId = "existing-session";
            var existingSession = new ChatSession { SessionId = sessionId, UserId = userId };

            _activeStoreMock
                .Setup(s => s.GetActiveSessionIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Success(sessionId));

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(existingSession));

            // Act
            var result = await _service.StartSessionAsync(userId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(sessionId, result.Value?.SessionId);
        }

        [Fact]
        public async Task StartSessionAsync_ShouldCreateNewSession_IfNoneExists()
        {
            // Arrange
            var userId = "user456";

            _activeStoreMock
                .Setup(s => s.GetActiveSessionIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Failure(""));

            _activeStoreMock
                .Setup(s => s.SaveSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            // Act
            var result = await _service.StartSessionAsync(userId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(userId, result.Value?.UserId);
        }

        [Fact]
        public async Task StartSessionAsync_ShouldFail_WhenSessionRetrievalFails()
        {
            // Arrange
            var userId = "user789";

            _activeStoreMock
                .Setup(s => s.GetActiveSessionIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Failure("Retrieval error"));

            _activeStoreMock
                .Setup(s => s.SaveSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure(""));

            // Act
            var result = await _service.StartSessionAsync(userId);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task StartSessionAsync_ShouldFail_WhenSessionSaveFails()
        {
            // Arrange
            var userId = "user101";

            _activeStoreMock
                .Setup(s => s.GetActiveSessionIdAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Failure(""));

            _activeStoreMock
                .Setup(s => s.SaveSessionAsync(It.IsAny<ChatSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure("Save failed"));

            // Act
            var result = await _service.StartSessionAsync(userId);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task EndSessionAsync_ShouldArchiveAndDeleteSession()
        {
            // Arrange
            var sessionId = "end-session";
            var session = new ChatSession { SessionId = sessionId, UserId = "user789" };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _archiveStoreMock
                .Setup(s => s.PersistSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            _activeStoreMock
                .Setup(s => s.DeleteSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            // Act
            var result = await _service.EndSessionAsync(sessionId);

            // Assert
            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task EndSessionAsync_ShouldFail_WhenArchiveFails()
        {
            // Arrange
            var sessionId = "archive-fail-session";
            var session = new ChatSession { SessionId = sessionId, UserId = "user102" };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _archiveStoreMock
                .Setup(s => s.PersistSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure("Archive failed"));

            // Act
            var result = await _service.EndSessionAsync(sessionId);

            // Assert
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task EndSessionAsync_ShouldFail_WhenDeleteFails()
        {
            // Arrange
            var sessionId = "delete-fail-session";
            var session = new ChatSession { SessionId = sessionId, UserId = "user103" };

            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(session));

            _archiveStoreMock
                .Setup(s => s.PersistSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            _activeStoreMock
                .Setup(s => s.DeleteSessionAsync(session, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure("Delete failed"));

            // Act
            var result = await _service.EndSessionAsync(sessionId);

            // Assert
            Assert.False(result.IsSuccess);
        }


        [Fact]
        public async Task EndSessionAsync_ShouldSucceed_IfSessionNotFound()
        {
            var sessionId = "missing-session";

            // Arrange
            _activeStoreMock
                .Setup(s => s.GetSessionAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Failure(""));

            // Act
            var result = await _service.EndSessionAsync(sessionId);

            // Assert
            Assert.True(result.IsSuccess);
        }

        
    }
}
