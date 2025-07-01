using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Services
{
    //Interface for session lifecycle (create, send, stream, end)
    public interface ISessionManager
    {
        Task<Result<ChatSession>> StartSessionAsync(string userId, CancellationToken ct = default);
        Task<Result<Unit>> SendMessageAsync(string sessionId, string message, CancellationToken ct = default);
        IAsyncEnumerable<string> StreamReplyAsync(string sessionId, CancellationToken cancellationToken); // keep as-is, or wrap each token in a `Result<string>` if needed
        Task<Result<Unit>> EndSessionAsync(string sessionId);
    }
}
