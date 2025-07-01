using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Contracts.Ports
{
    public interface IDistributedLockManager
    {
        Task<Result<string>> AcquireLockAsync(string lockKey, CancellationToken ct = default);
        Task<Result<Unit>> ReleaseLockAsync(string lockKey, string lockValue);
    }
}
