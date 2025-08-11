using FluentAssertions;
using LLMSessionGateway.Core;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Core
{
    public class ChatSessionServiceTests
    {
        private static ChatMessage Msg(string id, ChatRole role, string content, DateTime? ts = null) =>
            new ChatMessage
            {
                MessageId = id,
                Role = role,
                Content = content,
                Timestamp = ts ?? DateTime.UtcNow
            };

        [Fact]
        public void AddMessageIfAbsent_AddsMessage_WithCorrectRoleAndContent()
        {
            // Arrange
            var session = new ChatSession { SessionId = "s1", UserId = "u1" };
            var service = new ChatSessionService();
            var message = Msg("m1", ChatRole.User, "Hello");

            // Act
            var added = service.AddMessageIfAbsent(session, message);

            // Assert
            added.Should().BeTrue();
            session.Messages.Should().ContainSingle(m =>
                m.MessageId == "m1" &&
                m.Content == "Hello" &&
                m.Role == ChatRole.User
            );
        }

        [Fact]
        public void AddMessageIfAbsent_UpdatesLastInteraction_ToMessageTimestamp()
        {
            // Arrange
            var t = DateTime.UtcNow;
            var session = new ChatSession { SessionId = "s1", UserId = "u1" };
            var service = new ChatSessionService();
            var message = Msg("m1", ChatRole.Assistant, "Hi", t);

            // Act
            var added = service.AddMessageIfAbsent(session, message);

            // Assert
            added.Should().BeTrue();
            session.LastInteraction.Should().Be(t);
        }

        [Fact]
        public void AddMessageIfAbsent_DoesNotAdd_WhenMessageIdAlreadyExists()
        {
            // Arrange
            var session = new ChatSession { SessionId = "s1", UserId = "u1" };
            var service = new ChatSessionService();
            var first = Msg("m1", ChatRole.User, "Hello");
            var dup = Msg("m1", ChatRole.User, "Hello again");

            service.AddMessageIfAbsent(session, first);
            var beforeCount = session.Messages.Count;
            var beforeLastInteraction = session.LastInteraction;

            // Act
            var added = service.AddMessageIfAbsent(session, dup);

            // Assert
            added.Should().BeFalse();
            session.Messages.Count.Should().Be(beforeCount);          
            session.LastInteraction.Should().Be(beforeLastInteraction); 
        }

        [Fact]
        public void IsIdle_ReturnsTrue_WhenTimeoutExceeded()
        {
            // Arrange
            var session = new ChatSession
            {
                SessionId = "s1",
                UserId = "u1",
                LastInteraction = DateTime.UtcNow.AddMinutes(-10)
            };

            var service = new ChatSessionService();

            // Act
            var result = service.IsIdle(session, TimeSpan.FromMinutes(5));

            // Assert
            result.Should().BeTrue();
        }
    }
}
