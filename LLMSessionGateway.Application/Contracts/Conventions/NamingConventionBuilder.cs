namespace LLMSessionGateway.Application.Contracts.KeyGeneration
{
    public static class NamingConventionBuilder
    {
        public static string UserActiveKeyBuild(string userId) => $"chat_user:{userId}:active";
        public static string SessionKeyBuild(string sessionId) => $"chat_session:{sessionId}";
        public static string BlobSessionPathBuild(string userId, string sessionId, DateTime createdAt)
        => $"sessions/{userId}/{sessionId}/{createdAt:yyyyMMddHHmmss}.json";
        public static string LockKeyBuild(string userId) => $"chat_lock:{userId}";
        public static string TracingOperationNameBuild((string Source, string Operation) callerInfo)
                => $"{callerInfo.Source}.{callerInfo.Operation}";
    }
}
