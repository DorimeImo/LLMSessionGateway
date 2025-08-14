using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Contracts.Ports
{
    public interface IChatBackend
    {
        Task<Result<Unit>> OpenConnectionAsync(string sessionId, string userId, CancellationToken cancellationToken);
        Task<Result<Unit>> SendUserMessageAsync(string sessionId, string message, string messageId, CancellationToken cancellationToken);
        IAsyncEnumerable<string> StreamAssistantReplyAsync(string sessionId, string parentMessageId, CancellationToken cancellationToken);
        Task<Result<Unit>> CloseConnectionAsync(string sessionId);
    }
}
