using LLMSessionGateway.Application.Contracts.Commands;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System.Runtime.CompilerServices;
using System.Text;

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
            var persistenceResult = await _lifecycle.StartSessionAsync(userId, ct);
            if (persistenceResult.IsFailure)
                return persistenceResult;

            var backendResult = await _messaging.OpenConnectionAsync(persistenceResult.Value!.SessionId, userId, ct);
            if (backendResult.IsFailure)
            {
                await _lifecycle.EndSessionAsync(persistenceResult.Value!.SessionId, ct);

                return backendResult.MapUnitTo<ChatSession>(() => persistenceResult.Value);
            }
            return persistenceResult;
        }

        public async Task<Result<Unit>> SendMessageAsync(string sessionId, string message, string messageId, CancellationToken ct = default)
        {
            SendMessageCommand sendMessageCommand = new SendMessageCommand() { SessionId = sessionId, Message = message, MessageId = messageId };
            var updateResult = await _updater.AddMessageAsync(sendMessageCommand, ChatRole.User, ct);
            if (updateResult.IsFailure)
                return updateResult;

            return await _messaging.SendMessageAsync(sendMessageCommand, ct);
        }

        public async IAsyncEnumerable<string> StreamReplyAsync(string sessionId, string parentMessageId, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var sb = new StringBuilder();
            var completedNormally = false;


            try
            {
                await foreach (var chunk in _messaging
                    .StreamReplyAsync(sessionId, parentMessageId, ct)
                    .WithCancellation(ct))
                {
                    sb.Append(chunk);
                    yield return chunk;
                }

                completedNormally = true;
            }
            finally
            {
                if (completedNormally && sb.Length > 0)
                {
                    var assistantMessageId = Guid.NewGuid().ToString();

                    var cmd = new SendMessageCommand
                    {
                        SessionId = sessionId,
                        MessageId = assistantMessageId,
                        Message = sb.ToString()
                    };

                    var save = await _updater.AddMessageAsync(cmd, ChatRole.Assistant, ct);
                }
            }
        }

        public async Task<Result<Unit>> EndSessionAsync(string sessionId)
        {
            return await _lifecycle.EndSessionAsync(sessionId);
        }
    }
}
