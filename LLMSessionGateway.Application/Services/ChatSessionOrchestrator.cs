using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;

namespace LLMSessionGateway.Application.Services
{
    public class ChatSessionOrchestrator : IChatSessionOrchestrator
    {
        private readonly ISessionLifecycleService _lifecycle;
        private readonly IChatSessionUpdater _updater;
        private readonly ISessionMessagingService _messaging;

        public ChatSessionOrchestrator(
            ISessionLifecycleService lifecycle,
            IChatSessionUpdater updater,
            ISessionMessagingService messaging)
        {
            _lifecycle = lifecycle;
            _updater = updater;
            _messaging = messaging;
        }

        public async Task<Result<ChatSession>> StartSessionAsync(string userId, CancellationToken ct = default)
        {
            return await _lifecycle.StartSessionAsync(userId, ct);
        }

        public async Task<Result<Unit>> SendMessageAsync(string sessionId, string message, CancellationToken ct = default)
        {
            var updateResult = await _updater.AddMessageAsync(sessionId, ChatRole.User, message, ct);
            if (updateResult.IsFailure)
                return updateResult;

            return await _messaging.SendMessageAsync(sessionId, message, ct);
        }

        public IAsyncEnumerable<string> StreamReplyAsync(string sessionId, CancellationToken cancellationToken)
        {
            return _messaging.StreamReplyAsync(sessionId, cancellationToken);
        }

        public async Task<Result<Unit>> EndSessionAsync(string sessionId)
        {
            return await _lifecycle.EndSessionAsync(sessionId);
        }
    }
}
