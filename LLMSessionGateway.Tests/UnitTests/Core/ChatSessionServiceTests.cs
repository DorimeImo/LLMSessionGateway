using FluentAssertions;
using LLMSessionGateway.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Core
{
    public class ChatSessionServiceTests
    {
        [Fact]
        public void AddMessage_AddsCorrectRoleAndContent()
        {
            var session = new ChatSession { SessionId = "s1", UserId = "u1" };
            var service = new ChatSessionService();

            service.AddMessage(session, ChatRole.User, "Hello");

            session.Messages.Should().ContainSingle(m =>
                m.Content == "Hello" &&
                m.Role == ChatRole.User
            );
        }

        [Fact]
        public void AddMessage_UpdatesLastInteraction()
        {
            var session = new ChatSession { SessionId = "s1", UserId = "u1" };
            var service = new ChatSessionService();

            var before = DateTime.UtcNow;
            service.AddMessage(session, ChatRole.Assistant, "Hi");
            var after = DateTime.UtcNow;

            session.LastInteraction.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        }

        [Fact]
        public void IsIdle_ReturnsTrue_WhenTimeoutExceeded()
        {
            var session = new ChatSession
            {
                SessionId = "s1",
                UserId = "u1",
                LastInteraction = DateTime.UtcNow.AddMinutes(-10)
            };

            var service = new ChatSessionService();
            var result = service.IsIdle(session, TimeSpan.FromMinutes(5));

            result.Should().BeTrue();
        }
    }
}
