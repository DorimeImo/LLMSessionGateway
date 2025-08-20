using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.ActiveSessionStore.Redis
{
    public interface IDistributedLockManager
    {
        Task<Result<T>> RunWithLockAsync<T>(string lockKey, Func<CancellationToken, Task<Result<T>>> action, CancellationToken ct = default);
    }
}
