using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Services
{
    public interface ISessionLifecycleService
    {
        Task<Result<ChatSession>> StartSessionAsync(string userId, CancellationToken ct = default);
        Task<Result<Unit>> EndSessionAsync(string sessionId, CancellationToken ct = default);
    }
}
