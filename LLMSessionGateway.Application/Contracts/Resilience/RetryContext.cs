using LLMSessionGateway.Core.Utilities.Functional;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Application.Contracts.Resilience
{
    public record RetryContext<T>(
        int Attempt,
        TimeSpan Delay,
        Result<T> Result
    );
}
