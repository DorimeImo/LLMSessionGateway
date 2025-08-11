using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core
{
    public class ChatSessionService : IChatSessionService
    {
        public bool AddMessageIfAbsent(ChatSession session, ChatMessage message)
        {
            if (session.Messages.Any(m => m.MessageId == message.MessageId))
                return false;

            session.Messages.Add(message);
            Touch(session, message.Timestamp);
            return true;
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
