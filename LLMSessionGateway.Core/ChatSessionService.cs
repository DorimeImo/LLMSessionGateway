using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core
{
    public class ChatSessionService : IChatSessionService
    {
        public void AddMessage(ChatSession session, ChatRole role, string content)
        {
            var userMessage = new ChatMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            session.Messages.Add(userMessage);
            Touch(session, userMessage.Timestamp);
        }

        public bool IsIdle(ChatSession session, TimeSpan idleTimeout)
        {
            var now = DateTime.UtcNow;
            return now - session.LastInteraction > idleTimeout;
        }
        private void Touch(ChatSession session, DateTime? now = null)
        {
            session.LastInteraction = now ?? DateTime.UtcNow;
        }

    }
}
