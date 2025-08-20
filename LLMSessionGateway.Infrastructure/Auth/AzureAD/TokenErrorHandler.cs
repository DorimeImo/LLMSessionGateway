using Azure;
using Azure.Identity;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Auth.AzureAD
{
    public static class TokenErrorHandler
    {
        public static Result<T> Handle<T>(Exception ex, Serilog.ILogger logger)
        {
            switch (ex)
            {
                case OperationCanceledException:
                    logger.Warning("Azure Token acquisition was canceled.");
                    return Result<T>.Failure("Operation canceled", errorCode: "CANCELLED", isRetryable: false);

                case CredentialUnavailableException cue:
                    logger.Error("No usable credentials found for Azure token acquisition. Ensure Managed Identity / env credentials are configured.",
                        cue);
                    return Result<T>.Failure("Credential unavailable", errorCode: "CREDENTIAL_UNAVAILABLE", isRetryable: false);

                case AuthenticationFailedException afe:
                    logger.Error($"Authentication failed while acquiring Azure token. Message: {afe.Message}", afe);
                    return Result<T>.Failure("Authentication failed", errorCode: "AUTH_FAILED", isRetryable: true);

                case RequestFailedException rfe:
                    logger.Error($"Authority returned error. Status: {rfe.Status}, ErrorCode: {rfe.ErrorCode 
                        ?? "UNKNOWN"}, Message: {rfe.Message}",
                        rfe);
                    var retryable = rfe.Status is 408 or 429 or 500 or 502 or 503;
                    return Result<T>.Failure("Authority request failed", errorCode: rfe.ErrorCode 
                        ?? "AUTHORITY_ERROR", isRetryable: retryable);

                default:
                    throw ex;
            }
        }
    }
}
