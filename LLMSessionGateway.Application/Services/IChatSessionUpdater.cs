using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Services
{
    public interface IChatSessionUpdater
    {
        Task<Result<Unit>> AddMessageAsync(string sessionId, ChatRole role, string content, CancellationToken ct = default);
    }
}
