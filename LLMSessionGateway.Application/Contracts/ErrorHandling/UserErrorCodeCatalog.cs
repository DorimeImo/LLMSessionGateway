namespace LLMSessionGateway.Application.Contracts.ErrorHandling
{
    public static class UserErrorCodeCatalog
    {
        private static readonly Dictionary<string, string> _messages = new()
        {
            ["SESSION_NOT_FOUND"] = "The selected session is not available. Please choose a different session or start a new one.",
            ["CANCELLED"] = "The operation was cancelled. Please try again.",
            ["REDIS_LOCK_FAILED"] = "You already have an active session. Please end it before starting a new one.",
        };

        public static string GetMessage(string? errorCode)
        {
            return errorCode != null && _messages.TryGetValue(errorCode, out var msg)
                ? msg
                : "The service is temporarily unavailable. Please try again shortly.";
        }

        public static bool IsMapped(string? errorCode) =>
            errorCode != null && _messages.ContainsKey(errorCode);
    }
}
