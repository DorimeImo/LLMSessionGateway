using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Auth
{
    public interface ITokenProvider
    {
        Task<Result<string>> GetTokenAsync(string audienceAndScope, CancellationToken ct = default);
    }
}
