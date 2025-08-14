using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core.Utilities.Functional
{
    public static class ResultExtensions
    {
        public static Result<T> MapUnitTo<T>(this Result<Unit> r, Func<T> onSuccess) =>
            r.IsSuccess
                ? Result<T>.Success(onSuccess())
                : Result<T>.Failure(r.Error!, r.ErrorCode, r.IsRetryable);
    }
}
