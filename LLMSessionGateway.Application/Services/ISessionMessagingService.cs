using LLMSessionGateway.Application.Contracts.Commands;
using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Services
{
    public interface ISessionMessagingService
    {
        Task<Result<Unit>> SendMessageAsync(SendMessageCommand command, CancellationToken ct = default);
        IAsyncEnumerable<string> StreamReplyAsync(string sessionId, CancellationToken ct = default);
    }
}
