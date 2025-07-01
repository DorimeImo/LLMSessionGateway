using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Contracts.Ports
{
    public interface IArchiveSessionStore
    {
        Task<Result<Unit>> PersistSessionAsync(ChatSession session, CancellationToken ct = default);
        Task<Result<ChatSession>> GetSessionAsync(string userId, string sessionId, DateTime createdAt, CancellationToken ct = default);
        Task<Result<IEnumerable<(string sessionId, DateTime createdAt)>>> GetAllSessionIdsAsync(string userId, CancellationToken ct = default);
    }
}
