using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core
{
    public interface IChatSessionService
    {
        void AddMessage(ChatSession session, ChatRole role, string content);
        bool IsIdle(ChatSession session, TimeSpan idleTimeout);
    }
}
