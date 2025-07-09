namespace LLMSessionGateway.Application.Contracts.ErrorHandling
{
    public static class UserErrorCodeCatalog
    {
        private static readonly Dictionary<string, (string Message, int StatusCode)> _errorMap = new()
        {
            ["SESSION_NOT_FOUND"] = (
                "The selected session is not available. Please choose a different session or start a new one.",
                404
            ),
                    ["REDIS_LOCK_FAILED"] = (
                "You already have an active session. Please end it before starting a new one.",
                409
            ),
                    ["CANCELLED"] = (
                "The operation was cancelled. Please try again.",
                400
            )
        };

        public static (string Message, int StatusCode, string ErrorCode) GetErrorDetails(string? errorCode)
        {
            if (errorCode != null && _errorMap.TryGetValue(errorCode, out var tuple))
            {
                return (
                    Message: tuple.Message,
                    StatusCode: tuple.StatusCode,
                    ErrorCode: errorCode
                );
            }

            return (
                Message: "The service is temporarily unavailable. Please try again shortly.",
                StatusCode: 500,
                ErrorCode: "UNKNOWN"
            );
        }
    }
}
