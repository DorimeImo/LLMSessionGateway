using FluentAssertions;
using LLMSessionGateway.Application.Contracts.Commands;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Application
{
    public class ChatSessionOrchestratorTests
    {
        private readonly Mock<ISessionLifecycleService> _lifecycle = new();
        private readonly Mock<IChatSessionUpdater> _updater = new();
        private readonly Mock<ISessionMessagingService> _messaging = new();

        private ChatSessionOrchestrator CreateSut() =>
            new ChatSessionOrchestrator(_lifecycle.Object, _updater.Object, _messaging.Object);

        //StartSession

        [Fact]
        public async Task StartSession_Success_OpensBackendAndReturnsSession()
        {
            var session = new ChatSession { SessionId = "s1", UserId = "u1", CreatedAt = DateTime.UtcNow };
            _lifecycle.Setup(x => x.StartSessionAsync("u1", It.IsAny<CancellationToken>()))
                      .ReturnsAsync(Result<ChatSession>.Success(session));
            _messaging.Setup(x => x.OpenConnectionAsync("s1", "u1", It.IsAny<CancellationToken>()))
                      .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            var sut = CreateSut();
            var res = await sut.StartSessionAsync("u1");

            res.IsSuccess.Should().BeTrue();
            res.Value!.SessionId.Should().Be("s1");
            _lifecycle.Verify(x => x.StartSessionAsync("u1", It.IsAny<CancellationToken>()), Times.Once);
            _messaging.Verify(x => x.OpenConnectionAsync("s1", "u1", It.IsAny<CancellationToken>()), Times.Once);
            _lifecycle.Verify(
                x => x.EndSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task StartSession_BackendOpenFails_RollsBackAndReturnsFailure()
        {
            var session = new ChatSession { SessionId = "s1", UserId = "u1", CreatedAt = DateTime.UtcNow };
            _lifecycle.Setup(x => x.StartSessionAsync("u1", It.IsAny<CancellationToken>()))
                      .ReturnsAsync(Result<ChatSession>.Success(session));
            _messaging.Setup(x => x.OpenConnectionAsync("s1", "u1", It.IsAny<CancellationToken>()))
                      .ReturnsAsync(Result<Unit>.Failure("open failed", errorCode: "OPEN_ERR"));
            _lifecycle.Setup(
                x => x.EndSessionAsync("s1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            var sut = CreateSut();
            var res = await sut.StartSessionAsync("u1");

            res.IsFailure.Should().BeTrue();
            res.ErrorCode.Should().Be("OPEN_ERR");
            _lifecycle.Verify(
                x => x.EndSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        //SendMessage

        [Fact]
        public async Task SendMessage_Success_AddsUserMessage_ThenSendsToBackend()
        {
            var cmd = new SendMessageCommand { SessionId = "s1", MessageId = "m1", Message = "hi" };

            _updater
                .Setup(x => x.AddMessageAsync(
                    It.Is<SendMessageCommand>(c =>
                        c.SessionId == "s1" &&
                        c.MessageId == "m1" &&
                        c.Message == "hi"),
                    ChatRole.User,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            _messaging
                .Setup(x => x.SendMessageAsync(
                    It.Is<SendMessageCommand>(c =>
                        c.SessionId == "s1" &&
                        c.MessageId == "m1" &&
                        c.Message == "hi"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            var sut = CreateSut();

            var res = await sut.SendMessageAsync(cmd.SessionId, cmd.Message, cmd.MessageId);

            res.IsSuccess.Should().BeTrue();

            _updater.Verify(x => x.AddMessageAsync(
                It.Is<SendMessageCommand>(c =>
                    c.SessionId == "s1" &&
                    c.MessageId == "m1" &&
                    c.Message == "hi"),
                ChatRole.User,
                It.IsAny<CancellationToken>()), Times.Once);

            _messaging.Verify(x => x.SendMessageAsync(
                It.Is<SendMessageCommand>(c =>
                    c.SessionId == "s1" &&
                    c.MessageId == "m1" &&
                    c.Message == "hi"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SendMessage_WhenUpdaterFails_DoesNotCallBackend_ReturnsFailure()
        {
            // Arrange
            _updater.Setup(x => x.AddMessageAsync(
                    It.Is<SendMessageCommand>(c =>
                        c.SessionId == "s1" &&
                        c.MessageId == "m1" &&
                        c.Message == "hi"),
                    ChatRole.User,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Failure("dup", errorCode: "duplicate_message"));

            var sut = CreateSut();

            // Act
            var res = await sut.SendMessageAsync("s1", "hi", "m1");

            // Assert
            res.IsFailure.Should().BeTrue();
            res.ErrorCode.Should().Be("duplicate_message");

            _messaging.Verify(m => m.SendMessageAsync(
                It.IsAny<SendMessageCommand>(), It.IsAny<CancellationToken>()), Times.Never);

            _updater.Verify(x => x.AddMessageAsync(
                It.Is<SendMessageCommand>(c =>
                    c.SessionId == "s1" &&
                    c.MessageId == "m1" &&
                    c.Message == "hi"),
                ChatRole.User,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        //StreamReply 

        [Fact]
        public async Task StreamReply_HappyPath_PersistsAssistantOnce_WithConcatenatedText()
        {
            _messaging.Setup(x => x.StreamReplyAsync("s1", "m1", It.IsAny<CancellationToken>()))
                      .Returns(FakeStream("A", "B", "C"));

            SendMessageCommand? persisted = null;
            _updater.Setup(x => x.AddMessageAsync(It.IsAny<SendMessageCommand>(), ChatRole.Assistant, It.IsAny<CancellationToken>()))
                    .Callback<SendMessageCommand, ChatRole, CancellationToken>((c, _, __) => persisted = c)
                    .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            var sut = CreateSut();

            var chunks = new List<string>();
            await foreach (var chunk in sut.StreamReplyAsync("s1", "m1"))
                chunks.Add(chunk);

            chunks.Should().Equal("A", "B", "C");
            persisted.Should().NotBeNull();
            persisted!.SessionId.Should().Be("s1");
            persisted.Message.Should().Be("ABC");
            persisted.MessageId.Should().NotBeNullOrEmpty();
            _updater.Verify(x => x.AddMessageAsync(It.IsAny<SendMessageCommand>(), ChatRole.Assistant, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StreamReply_EmptyStream_DoesNotPersist()
        {
            _messaging.Setup(x => x.StreamReplyAsync("s1", "m1", It.IsAny<CancellationToken>()))
                      .Returns(FakeStream());

            var sut = CreateSut();

            await foreach (var _ in sut.StreamReplyAsync("s1", "m1")) {}

            _updater.Verify(x => x.AddMessageAsync(It.IsAny<SendMessageCommand>(), ChatRole.Assistant, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StreamReply_BackendThrows_NoPersist()
        {
            _messaging.Setup(x => x.StreamReplyAsync("s1", "m1", It.IsAny<CancellationToken>()))
                      .Returns(ThrowingStream(new Exception("boom"), beforeYieldCount: 1));

            var sut = CreateSut();

            Func<Task> act = async () =>
            {
                await foreach (var _ in sut.StreamReplyAsync("s1", "m1")) {}
            };

            await act.Should().ThrowAsync<Exception>().WithMessage("boom");
            _updater.Verify(x => x.AddMessageAsync(It.IsAny<SendMessageCommand>(), ChatRole.Assistant, It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StreamReply_Cancelled_NoPersist()
        {
            var cts = new CancellationTokenSource();
            _messaging.Setup(x => x.StreamReplyAsync("s1", "m1", It.IsAny<CancellationToken>()))
                      .Returns(CancellableStream(cts.Token));

            var sut = CreateSut();

            var first = true;
            Func<Task> act = async () =>
            {
                await foreach (var _ in sut.StreamReplyAsync("s1", "m1", cts.Token))
                {
                    if (first) { first = false; cts.Cancel(); }
                }
            };

            await act.Should().ThrowAsync<TaskCanceledException>();
            _updater.Verify(x => x.AddMessageAsync(It.IsAny<SendMessageCommand>(), ChatRole.Assistant, It.IsAny<CancellationToken>()), Times.Never);
        }

        //Helpers

        private static async IAsyncEnumerable<string> FakeStream(params string[] chunks)
        {
            foreach (var c in chunks)
            {
                yield return c;
                await Task.Yield();
            }
        }

        private static async IAsyncEnumerable<string> ThrowingStream(Exception ex, int beforeYieldCount)
        {
            int yielded = 0;
            while (yielded < beforeYieldCount)
            {
                yield return $"chunk{yielded}";
                yielded++;
                await Task.Yield();
            }
            throw ex;
        }

        private static async IAsyncEnumerable<string> CancellableStream(CancellationToken ct)
        {
            // yield once then wait forever (until canceled)
            yield return "first";
            await Task.Delay(Timeout.Infinite, ct);
            yield return "never"; // unreachable
        }
    }
}
