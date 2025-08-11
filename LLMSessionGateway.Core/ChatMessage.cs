using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core
{
    public class ChatMessage
    {
        public required string MessageId { get; init; } 
        public ChatRole Role { get; init; }
        public required string Content { get; init; } 
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}
