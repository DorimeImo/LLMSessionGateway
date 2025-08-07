using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LLMSessionGateway.Core;
using LLMSessionGateway.Infrastructure.AzureBlobStorage;
using LLMSessionGateway.Tests.UnitTests.Infrastructure.Helpers;
using Moq;
using Observability.Shared.Contracts;
using System.IO.Compression;
using System.Text.Json;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Infrastructure.Blob
{
    public class BlobSessionStoreTests
    {
        // -----------------------------------------
        // ✅ Core success and expected flow tests
        // -----------------------------------------

        [Fact]
        public async Task PersistSessionAsync_WritesCorrectBlobName()
        {
            // Arrange
            var session = CreateSession();
            string? capturedBlobName = null;
            var store = CreateStore(out var mockBlobClient, out var mockContainerClient);

            mockContainerClient
                .Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Callback<string>(name => capturedBlobName = name)
                .Returns(mockBlobClient.Object);

            mockBlobClient
                .Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

            // Act
            var result = await store.PersistSessionAsync(session, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            var expectedBlobName = $"sessions/{session.UserId}/{session.SessionId}/{session.CreatedAt:yyyyMMddHHmmss}.json";
            Assert.Equal(expectedBlobName, capturedBlobName);
        }

        [Fact]
        public async Task PersistSessionAsync_CallsUploadAsyncOnce()
        {
            // Arrange
            var store = CreateStore(out var mockBlobClient, out _);
            var session = CreateSession();

            // Act
            var result = await store.PersistSessionAsync(session, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            mockBlobClient.Verify(
                b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetSessionAsync_WhenBlobExists_ReturnsDeserializedAndDecompressedSession()
        {
            // Arrange
            var session = CreateSession();
            var json = JsonSerializer.Serialize(session);

            var memoryStream = new MemoryStream();
            using (var gzip = new GZipStream(memoryStream, CompressionLevel.Optimal, true))
            using (var writer = new StreamWriter(gzip))
            {
                writer.Write(json);
            }
            memoryStream.Position = 0;

            var blobName = $"sessions/{session.UserId}/{session.SessionId}/{session.CreatedAt:yyyyMMddHHmmss}.json";
            var store = CreateStore(out var mockBlobClient, out var mockContainerClient);

            mockContainerClient.Setup(c => c.GetBlobClient(blobName)).Returns(mockBlobClient.Object);
            mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            mockBlobClient.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(
                    BlobsModelFactory.BlobDownloadStreamingResult(content: memoryStream),
                    Mock.Of<Response>()));

            // Act
            var result = await store.GetSessionAsync(session.UserId, session.SessionId, session.CreatedAt, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(session.SessionId, result.Value.SessionId);
            Assert.Equal(session.UserId, result.Value.UserId);
        }

        [Fact]
        public async Task GetSessionAsync_WhenBlobDoesNotExist_ReturnsFailure()
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var blobName = $"sessions/u1/s1/{createdAt:yyyyMMddHHmmss}.json";
            var store = CreateStore(out var mockBlobClient, out var mockContainerClient);

            mockContainerClient.Setup(c => c.GetBlobClient(blobName)).Returns(mockBlobClient.Object);
            mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

            // Act
            var result = await store.GetSessionAsync("u1", "s1", createdAt, CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
        }

        [Fact]
        public async Task GetSessionAsync_WhenStreamIsCorrupted_ReturnsFailure()
        {
            // Arrange
            var corruptedStream = new MemoryStream(new byte[] { 0x00, 0x01, 0x02 });
            var store = CreateStore(out var mockBlobClient, out _);

            mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            mockBlobClient.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(
                    BlobsModelFactory.BlobDownloadStreamingResult(content: corruptedStream),
                    Mock.Of<Response>()));

            // Act
            var result = await store.GetSessionAsync("u1", "s1", DateTime.UtcNow, CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("COMPRESSION_DECOMPRESSION_INVALID_DATA", result.ErrorCode);
        }

        // -----------------------------------------
        // ❌ Exception handling and logging tests
        // -----------------------------------------

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task PersistSessionAsync_HandlesExpectedExceptions(Exception ex, string expectedErrorCode, bool isRetryable)
        {
            // Arrange
            var loggerMock = new Mock<IStructuredLogger>();
            var store = CreateStore(out var mockBlobClient, out _, loggerMock);
            var session = CreateSession();

            mockBlobClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>())).ThrowsAsync(ex);

            // Act
            var result = await store.PersistSessionAsync(session, CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(expectedErrorCode, result.ErrorCode);
            Assert.Equal(isRetryable, result.IsRetryable);
            loggerMock.VerifyAnyLogging(ex);
        }

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task GetSessionAsync_HandlesExpectedExceptions(Exception ex, string expectedErrorCode, bool isRetryable)
        {
            // Arrange
            var createdAt = DateTime.UtcNow;
            var blobName = $"sessions/u1/s1/{createdAt:yyyyMMddHHmmss}.json";
            var loggerMock = new Mock<IStructuredLogger>();
            var store = CreateStore(out var mockBlobClient, out var mockContainerClient, loggerMock);

            mockContainerClient.Setup(c => c.GetBlobClient(blobName)).Returns(mockBlobClient.Object);
            mockBlobClient.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
            mockBlobClient.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(ex);

            // Act
            var result = await store.GetSessionAsync("u1", "s1", createdAt, CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(expectedErrorCode, result.ErrorCode);
            Assert.Equal(isRetryable, result.IsRetryable);
            loggerMock.VerifyAnyLogging(ex);
        }

        [Theory]
        [MemberData(nameof(GetAllSessionIdsAsync_ExceptionCases))]
        public async Task GetAllSessionIdsAsync_HandlesExpectedExceptions(Exception ex, string expectedErrorCode, bool isRetryable)
        {
            // Arrange
            var loggerMock = new Mock<IStructuredLogger>();
            var store = CreateStore(out _, out var mockContainerClient, loggerMock);

            mockContainerClient.Setup(c => c.GetBlobsAsync(
                    It.IsAny<BlobTraits>(),
                    It.IsAny<BlobStates>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Throws(ex);

            // Act
            var result = await store.GetAllSessionIdsAsync("u1", CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Contains(expectedErrorCode, result.ErrorCode);
            Assert.Equal(isRetryable, result.IsRetryable);
            loggerMock.VerifyAnyLogging(ex);
        }

        // -----------------------------------------
        // 🛠️ Helpers
        // -----------------------------------------

        private AzureBlobArchiveStore CreateStore(
            out Mock<BlobClient> mockBlobClient,
            out Mock<BlobContainerClient> mockContainerClient,
            Mock<IStructuredLogger>? loggerMock = null)
        {
            mockBlobClient = new Mock<BlobClient>();
            mockBlobClient.Setup(b => b.UploadAsync(It.IsAny<Stream>(), true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

            mockContainerClient = new Mock<BlobContainerClient>();
            mockContainerClient.Setup(c => c.GetBlobClient(It.IsAny<string>()))
                .Returns(mockBlobClient.Object);

            var mockServiceClient = new Mock<BlobServiceClient>();
            mockServiceClient.Setup(s => s.GetBlobContainerClient(It.IsAny<string>()))
                .Returns(mockContainerClient.Object);

            return new AzureBlobArchiveStore(
                mockServiceClient.Object,
                "test-container",
                loggerMock?.Object ?? new Mock<IStructuredLogger>().Object,
                new Mock<ITracingService>().Object);
        }

        private ChatSession CreateSession() =>
            new()
            {
                SessionId = "s1",
                UserId = "u1",
                CreatedAt = DateTime.UtcNow,
                Messages = new List<ChatMessage>
                {
                new() { Role = ChatRole.User, Content = "Hello", Timestamp = DateTime.UtcNow }
                }
            };

        public static IEnumerable<object[]> ExceptionTestData => new List<object[]>
            {
                new object[] { new RequestFailedException(500, "Simulated failure", "REQUEST_FAILED", null), "REQUEST_FAILED", true },
                new object[] { new OperationCanceledException(), "CANCELLED", false },
                new object[] { new JsonException("json error"), "JSON_ERROR", false },
                new object[] { new InvalidDataException("corrupted"), "COMPRESSION_DECOMPRESSION_INVALID_DATA", false },
                new object[] { new IOException("io error"), "IO_ERROR", true }
            };

        public static IEnumerable<object[]> GetAllSessionIdsAsync_ExceptionCases()
        {
            yield return new object[]
            {
            new RequestFailedException(500, "Simulated failure", "REQUEST_FAILED", null),
            "REQUEST_FAILED",
            true
            };
            yield return new object[]
            {
            new OperationCanceledException(),
            "CANCELLED",
            false
            };
        }
    }
}
