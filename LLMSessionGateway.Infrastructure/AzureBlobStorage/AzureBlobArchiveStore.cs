using Azure.Storage.Blobs;
using LLMSessionGateway.Application.Contracts.KeyGeneration;
using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Application.Contracts.Observability;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using System.IO.Compression;
using System.Text.Json;

namespace LLMSessionGateway.Infrastructure.AzureBlobStorage
{
    public class AzureBlobArchiveStore : IArchiveSessionStore
    {
        private readonly BlobContainerClient _containerClient;
        private readonly IStructuredLogger _logger;
        private readonly ITracingService _tracingService;

        public AzureBlobArchiveStore(BlobServiceClient blobServiceClient, string containerName, IStructuredLogger logger, ITracingService tracingService)
        {
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            _logger = logger;
            _tracingService = tracingService;
        }

        public async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<Result<Unit>> PersistSessionAsync(ChatSession session, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    var blobName = NamingConventionBuilder.BlobSessionPathBuild(session.UserId, session.SessionId, session.CreatedAt);
                    var blobClient = _containerClient.GetBlobClient(blobName);

                    var sessionBytes = JsonSerializer.SerializeToUtf8Bytes(session);
                    using var compressedStream = new MemoryStream();
                    using (var gzip = new GZipStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
                    {
                        await gzip.WriteAsync(sessionBytes, cancellationToken);
                    }
                    compressedStream.Position = 0;

                    await blobClient.UploadAsync(compressedStream, overwrite: true, cancellationToken);

                    return Result<Unit>.Success(Unit.Value);
                }
                catch (Exception ex)
                {
                    return AzureBlobErrorHandler.Handle<Unit>(ex, source, operation, _logger);
                }
            }
        }

        public async Task<Result<ChatSession>> GetSessionAsync(string userId, string sessionId, DateTime createdAt, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                try
                {
                    var blobName = NamingConventionBuilder.BlobSessionPathBuild(userId, sessionId, createdAt);
                    var blobClient = _containerClient.GetBlobClient(blobName);

                    if (!await blobClient.ExistsAsync(cancellationToken))
                        return Result<ChatSession>.Failure("Session not found", errorCode: "SESSION_NOT_FOUND");

                    var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
                    using var gzip = new GZipStream(download.Value.Content, CompressionMode.Decompress);
                    using var reader = new StreamReader(gzip);
                    var json = await reader.ReadToEndAsync(cancellationToken);

                    var session = JsonSerializer.Deserialize<ChatSession>(json);
                    return Result<ChatSession>.Success(session!);
                }
                catch (Exception ex)
                {
                    return AzureBlobErrorHandler.Handle<ChatSession>(ex, source, operation, _logger);
                }
            }  
        }

        public async Task<Result<IEnumerable<(string sessionId, DateTime createdAt)>>> GetAllSessionIdsAsync(string userId, CancellationToken cancellationToken)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = NamingConventionBuilder.TracingOperationNameBuild((source, operation));

            using (_tracingService.StartActivity(tracingOperationName))
            {
                var prefix = $"sessions/{userId}/";
                var sessionInfos = new List<(string sessionId, DateTime createdAt)>();

                try
                {
                    await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
                    {
                        try
                        {
                            var parts = blobItem.Name.Split('/');
                            if (parts.Length < 4)
                            {
                                _logger.LogWarning(source, operation, $"Skipped malformed blob name: {blobItem.Name}");
                                continue;
                            }

                            var sessionId = parts[2];
                            var timestampPart = Path.GetFileNameWithoutExtension(parts[3]);

                            if (!DateTime.TryParseExact(timestampPart, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var createdAt))
                            {
                                _logger.LogWarning(source, operation, $"Skipped blob with unparsable timestamp: {blobItem.Name}");
                                continue;
                            }

                            sessionInfos.Add((sessionId, createdAt));
                        }
                        catch (Exception exInner)
                        {
                            _logger.LogWarning(source, operation, $"Skipped blob due to unexpected parsing error: {blobItem.Name}", exInner);
                            continue;
                        }
                    }

                    return Result<IEnumerable<(string sessionId, DateTime createdAt)>>.Success(sessionInfos.OrderByDescending(x => x.createdAt));
                }
                catch (Exception ex)
                {
                    return AzureBlobErrorHandler.Handle<IEnumerable<(string sessionId, DateTime createdAt)>>(ex, source, operation, _logger);
                }
            }     
        }
    }
}
