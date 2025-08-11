using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Contracts.Commands
{
    public sealed class SendMessageCommand
    {
        public required string SessionId { get; init; }
        public required string Message { get; init; }
        public required string MessageId { get; init; }
    }
}
