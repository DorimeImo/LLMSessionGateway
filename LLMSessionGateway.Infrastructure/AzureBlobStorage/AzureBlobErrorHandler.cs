using Azure;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using System.Text.Json;

namespace LLMSessionGateway.Infrastructure.AzureBlobStorage
{
    public static class AzureBlobErrorHandler
    {
        public static Result<T> Handle<T>(Exception ex, string source, string operation, IStructuredLogger logger)
        {
            switch (ex)
            {
                case RequestFailedException azureEx:
                    var azStatusCode = azureEx.Status;
                    var azErrorCode = azureEx.ErrorCode ?? "UNKNOWN_AZURE_ERROR";

                    logger.LogError(source, operation,
                        $"Azure Blob Storage error. Status: {azStatusCode}, ErrorCode: {azErrorCode}, Message: {azureEx.Message}", azureEx);

                    bool isAzureRetryable = azStatusCode is 408 or 429 or 500 or 503;

                    return Result<T>.Failure(
                        $"Azure Blob Storage error ({azErrorCode})",
                        errorCode: azErrorCode,
                        isRetryable: isAzureRetryable);

                case OperationCanceledException cancelEx:
                    logger.LogWarning(source, operation, "Operation was canceled.");
                    return Result<T>.Failure("Operation canceled", errorCode: "CANCELLED", isRetryable: false);

                case JsonException jsonEx:
                    logger.LogError(source, operation, "Json error.", jsonEx);
                    return Result<T>.Failure("Serialization or Deserialization error", errorCode: "JSON_ERROR", isRetryable: false);

                case InvalidDataException decompressEx:
                    logger.LogError(source, operation, "Operation failed due to invalid compressed data.", decompressEx);
                    return Result<T>.Failure("Compression or Decompresssion error", errorCode: "COMPRESSION_DECOMPRESSION_INVALID_DATA", isRetryable: false);

                case IOException ioEx:
                    logger.LogError(source, operation, "I/O error during operation.", ioEx);
                    return Result<T>.Failure("I/O error", errorCode: "IO_ERROR", isRetryable: true);

                default:
                    throw ex;
            }
        }
    }
}
