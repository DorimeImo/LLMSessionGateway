using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Contracts.Ports
{
    public interface IActiveSessionStore
    {
        Task<Result<string>> GetActiveSessionIdAsync(string userId, CancellationToken ct = default);
        Task<Result<ChatSession>> GetSessionAsync(string sessionId, CancellationToken ct = default);
        Task<Result<Unit>> SaveSessionAsync(ChatSession session, CancellationToken ct = default);
        Task<Result<Unit>> DeleteSessionAsync(ChatSession session, CancellationToken ct = default);
    }
}
