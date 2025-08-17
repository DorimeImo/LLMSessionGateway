using Grpc.Core;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using System.Runtime.CompilerServices;

namespace LLMSessionGateway.Infrastructure.Grpc
{
    public class GrpcChatBackend : IChatBackend
    {
        private readonly ChatService.ChatServiceClient _grpcClient;
        private readonly IStructuredLogger _logger;
        private readonly ITracingService _tracingService;
        private readonly GrpcTimeoutsConfigs _timeouts;

        public GrpcChatBackend(
            ChatService.ChatServiceClient grpcClient,
            IStructuredLogger logger,
            ITracingService tracingService,
            GrpcTimeoutsConfigs timeouts)
        {
            _grpcClient = grpcClient;
            _logger = logger;
            _tracingService = tracingService;
            _timeouts = timeouts;
        }

        public async Task<Result<Unit>> OpenConnectionAsync(string sessionId, string userId, CancellationToken ct)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            using (_tracingService.StartActivity(TracingOperationNameBuilder.TracingOperationNameBuild((source, operation))))
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    var headers = new Metadata
            {
                { "x-session-id", sessionId },
                { "x-user-id", userId }
            };

                    var deadline = DateTime.UtcNow.Add(TimeSpan.FromSeconds(_timeouts.OpenSeconds));
                    var request = new OpenSessionRequest { SessionId = sessionId, UserId = userId };

                    await _grpcClient.OpenSessionAsync(request,
                        headers: headers, deadline: deadline, cancellationToken: ct).ConfigureAwait(false);

                    return Result<Unit>.Success(Unit.Value);
                }
                catch (Exception ex)
                {
                    return GrpcErrorHandler.Handle<Unit>(ex, source, operation, _logger);
                }
            }
        }

        public async Task<Result<Unit>> SendUserMessageAsync(string sessionId, string message, string messageId, CancellationToken ct)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            using (_tracingService.StartActivity(TracingOperationNameBuilder.TracingOperationNameBuild((source, operation))))
            {
                try
                {
                    ct.ThrowIfCancellationRequested();

                    var headers = new Metadata
            {
                { "x-session-id", sessionId },
                { "x-message-id", messageId }
            };

                    var deadline = DateTime.UtcNow.Add(TimeSpan.FromSeconds(_timeouts.SendSeconds));

                    await _grpcClient.SendMessageAsync(new UserMessageRequest
                    {
                        SessionId = sessionId,
                        Message = message,
                        MessageId = messageId
                    }, headers: headers, deadline: deadline, cancellationToken: ct).ConfigureAwait(false);

                    return Result<Unit>.Success(Unit.Value);
                }
                catch (Exception ex)
                {
                    return GrpcErrorHandler.Handle<Unit>(ex, source, operation, _logger);
                }
            }
        }

        public async IAsyncEnumerable<string> StreamAssistantReplyAsync(string sessionId, string parentMessageId, [EnumeratorCancellation] CancellationToken ct)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            using (_tracingService.StartActivity(TracingOperationNameBuilder.TracingOperationNameBuild((source, operation))))
            {
                AsyncServerStreamingCall<AssistantReplyToken>? call = null;
                try
                {
                    ct.ThrowIfCancellationRequested();
                    var headers = new Metadata { { "x-session-id", sessionId }, { "x-message-id", parentMessageId } };
                    var setupDeadline = DateTime.UtcNow.Add(TimeSpan.FromSeconds(_timeouts.StreamSetupSeconds));

                    call = _grpcClient.StreamReply(new StreamReplyRequest { SessionId = sessionId, MessageId = parentMessageId },
                                                   headers: headers, deadline: setupDeadline, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    var reason = ex switch
                    {
                        TaskCanceledException => "Streaming initialization was canceled via token.",
                        RpcException rpc when rpc.StatusCode == StatusCode.Cancelled => "Streaming initialization was cancelled by client (gRPC Cancelled).",
                        RpcException rpc when rpc.StatusCode == StatusCode.DeadlineExceeded => "Streaming initialization timed out (gRPC DeadlineExceeded).",
                        RpcException rpc => $"gRPC error while initializing stream (StatusCode: {rpc.StatusCode}).",
                        _ => "Unhandled error while initializing streaming."
                    };
                    _logger.LogWarning(source, operation, reason, ex);
                    yield break;
                }

                await foreach (var token in StreamSafe(call.ResponseStream, ct))
                {
                    yield return token;
                }

                call?.Dispose();
            }
        }

        public async Task<Result<Unit>> CloseConnectionAsync(string sessionId)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            using (_tracingService.StartActivity(TracingOperationNameBuilder.TracingOperationNameBuild((source, operation))))
            {
                try
                {
                    var deadline = DateTime.UtcNow.Add(TimeSpan.FromSeconds(_timeouts.CloseSeconds));
                    await _grpcClient.CloseSessionAsync(new CloseSessionRequest { SessionId = sessionId },
                                                        deadline: deadline).ConfigureAwait(false);
                    return Result<Unit>.Success(Unit.Value);
                }
                catch (Exception ex)
                {
                    return GrpcErrorHandler.Handle<Unit>(ex, source, operation, _logger);
                }
            }
        }

        private async IAsyncEnumerable<string> StreamSafe(
            IAsyncStreamReader<AssistantReplyToken> stream,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();

            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await stream.MoveNext(ct);
                }
                catch (RpcException ex) when (ex.StatusCode is StatusCode.Cancelled or StatusCode.DeadlineExceeded)
                {
                    _logger.LogWarning(source, operation, $"Streaming was aborted by the client or timed out (gRPC status: {ex.StatusCode}).", ex);
                    yield break;
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning(source, operation, "Streaming was aborted by the client (TaskCanceledException).", ex);
                    yield break;
                }
                catch (Exception)
                {
                    throw;
                }

                if (!hasNext)
                    yield break;

                yield return stream.Current.Token;
            }
        }
    }
}
