using Grpc.Core;
using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Core.Utilities.Functional;

namespace LLMSessionGateway.Infrastructure.Grpc
{
    public static class GrpcErrorHandler
    {
        public static Result<T> Handle<T>(Exception ex, string source, string operation, IStructuredLogger logger)
        {
            switch (ex)
            {
                case RpcException rpc:
                    logger.LogWarning(source, operation,
                        $"Grpc failed with status: {rpc.StatusCode}, detail: {rpc.Status.Detail}", rpc);

                    bool isRetryable = rpc.StatusCode is StatusCode.Unavailable or StatusCode.DeadlineExceeded;
                    return Result<T>.Failure("gRPC error", "GRPC_ERROR", isRetryable: isRetryable);

                case TaskCanceledException canceledEx:
                    logger.LogWarning(source, operation, "Request was canceled.", canceledEx);
                    return Result<T>.Failure("Request canceled", "CANCELLED", isRetryable: false);

                default:
                    throw ex;
            }
        }
    }
}
