using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core
{
    public class ChatSession
    {
        public required string SessionId { get; init; }
        public required string UserId { get; init; }
        public string Version { get; set; } = "v1";
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public DateTime LastInteraction { get; private set; } = DateTime.UtcNow;
        public List<ChatMessage> Messages { get; init; } = new();
        public string? AssignedModelInstance { get; set; }

        public bool IsIdle(TimeSpan idleTimeout) =>
        DateTime.UtcNow - LastInteraction > idleTimeout;

        public void AddMessage(ChatMessage message)
        {
            Messages.Add(message);
            Touch();
        }

        private void Touch()
        {
            LastInteraction = DateTime.UtcNow;
        }
    }
}
