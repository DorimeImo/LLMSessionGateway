using FluentAssertions;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Tests.UnitTests.Application.Helpers;
using Moq;
using Observability.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Application
{
    public class SessionMessagingServiceTests
    {
        private readonly Mock<IChatBackend> _chatBackendMock = new();
        private readonly Mock<IRetryPolicyRunner> _retryRunnerMock = new();
        private readonly Mock<ITracingService> _tracingMock = new();
        private readonly Mock<IStructuredLogger> _loggerMock = new();

        private readonly SessionMessagingService _service;

        public SessionMessagingServiceTests()
        {
            _service = new SessionMessagingService(
                _chatBackendMock.Object,
                _retryRunnerMock.Object,
                _tracingMock.Object,
                _loggerMock.Object
            );
            RetryMockExtensions.SetupRetryRunnerMock(_retryRunnerMock);
        }

        [Fact]
        public async Task SendMessageAsync_ShouldReturnSuccess_WhenBackendSucceeds()
        {
            // Arrange
            var sessionId = "s1";
            var message = "hello";

            _chatBackendMock
                .Setup(b => b.SendUserMessageAsync(sessionId, message, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            // Act
            var result = await _service.SendMessageAsync(sessionId, message);

            // Assert
            result.IsSuccess.Should().BeTrue();
            _chatBackendMock.Verify(b => b.SendUserMessageAsync(sessionId, message, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendMessageAsync_ShouldReturnFailure_WhenBackendFails()
        {
            // Arrange
            var sessionId = "s1";
            var message = "fail";

            _chatBackendMock
                .Setup(b => b.SendUserMessageAsync(sessionId, message, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure("backend error"));

            // Act
            var result = await _service.SendMessageAsync(sessionId, message);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Should().Be("backend error");
        }

        [Fact]
        public void StreamReplyAsync_ShouldDelegateToBackend()
        {
            // Arrange
            var sessionId = "s1";
            var stream = GetAsyncStream();

            _chatBackendMock
                .Setup(b => b.StreamAssistantReplyAsync(sessionId, It.IsAny<CancellationToken>()))
                .Returns(stream);

            // Act
            var result = _service.StreamReplyAsync(sessionId);

            // Assert
            result.Should().BeSameAs(stream);
        }

        private async IAsyncEnumerable<string> GetAsyncStream()
        {
            yield return "message 1";
            yield return "message 2";
            await Task.CompletedTask;
        }
    }
}
