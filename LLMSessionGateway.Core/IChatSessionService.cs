using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core
{
    public interface IChatSessionService
    {
        bool AddMessageIfAbsent(ChatSession session, ChatMessage message);
        bool IsIdle(ChatSession session, TimeSpan idleTimeout);
    }
}
