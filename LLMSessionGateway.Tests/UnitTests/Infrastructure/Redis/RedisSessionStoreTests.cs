using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Infrastructure.Redis;
using LLMSessionGateway.Tests.UnitTests.Infrastructure.Helpers;
using Moq;
using Observability.Shared.Contracts;
using StackExchange.Redis;
using System.Text.Json;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Infrastructure.Redis
{
    public class RedisSessionStoreTests
    {
        private readonly Mock<IDatabase> _mockDb;
        private readonly Mock<IStructuredLogger> _loggerMock;
        private readonly Mock<IDistributedLockManager> _lockManagerMock;
        private readonly RedisActiveStore _store;
        private readonly TimeSpan _ttl = TimeSpan.FromMinutes(30);

        public RedisSessionStoreTests()
        {
            _mockDb = new Mock<IDatabase>();
            _loggerMock = new Mock<IStructuredLogger>();
            _lockManagerMock = new Mock<IDistributedLockManager>();

            var mockConnection = new Mock<IConnectionMultiplexer>();
            mockConnection
                .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDb.Object);

            _store = new RedisActiveStore(
                mockConnection.Object,
                _ttl,
                _loggerMock.Object,
                new Mock<ITracingService>().Object,
                _lockManagerMock.Object
            );

            SetupLockManagerMock(Result<string>.Success("mock"));
            SetupLockManagerMock(Result<ChatSession>.Success(new ChatSession { SessionId = "mock", UserId = "mock" }));
            SetupLockManagerMock(Result<Unit>.Success(Unit.Value));
        }

            // -----------------------------------------
            // ✅ Core success and expected flow tests
            // -----------------------------------------

            [Fact]
        public async Task GetSessionAsync_ReturnsDeserializedObject_WhenSessionExists()
        {
            // Arrange
            var session = CreateSession();
            var json = JsonSerializer.Serialize(session);
            _mockDb.Setup(db => db.StringGetAsync($"chat_session:{session.SessionId}", It.IsAny<CommandFlags>()))
                   .ReturnsAsync((RedisValue)json);

            // Act
            var result = await _store.GetSessionAsync(session.SessionId, CancellationToken.None);

            // Assert
            Assert.NotNull(result.Value);
            Assert.True(result.IsSuccess);
            Assert.Equal(session.SessionId, result.Value.SessionId);
        }

        [Fact]
        public async Task GetActiveSessionIdAsync_ReturnsValue_WhenExists()
        {
            // Arrange
            _mockDb.Setup(db => db.StringGetAsync("chat_user:u1:active", It.IsAny<CommandFlags>()))
                   .ReturnsAsync((RedisValue)"s1");

            // Act
            var result = await _store.GetActiveSessionIdAsync("u1", CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("s1", result.Value);
        }

        [Fact]
        public async Task SaveSessionAsync_StoresSessionAndActiveId_WithCorrectTTLAndKeys()
        {
            // Arrange
            var tranMock = new Mock<ITransaction>();
            _mockDb.Setup(db => db.CreateTransaction(null)).Returns(tranMock.Object);
            tranMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(true);

            var session = CreateSession();

            // Act
            var result = await _store.SaveSessionAsync(session, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            tranMock.Verify(t =>
                t.StringSetAsync($"chat_session:{session.SessionId}", It.IsAny<RedisValue>(), _ttl, false, When.Always, CommandFlags.None), Times.Once);

            tranMock.Verify(t =>
                t.StringSetAsync($"chat_user:{session.UserId}:active", session.SessionId, _ttl, false, When.Always, CommandFlags.None), Times.Once);

            tranMock.Verify(t => t.ExecuteAsync(It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task DeleteSessionAsync_DeletesCorrectKeyAndRunsConditionalScript()
        {
            // Arrange
            var session = CreateSession();
            _mockDb.Setup(db => db.KeyDeleteAsync($"chat_session:{session.SessionId}", CommandFlags.None)).ReturnsAsync(true);

            object? luaArgs = null;
            _mockDb.Setup(db => db.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), It.IsAny<CommandFlags>()))
                   .Callback<LuaScript, object, CommandFlags>((_, args, _) => luaArgs = args)
                   .ReturnsAsync(RedisResult.Create(1));

            // Act
            var result = await _store.DeleteSessionAsync(session, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);

            var props = luaArgs!.GetType().GetProperties();
            var keys = props.FirstOrDefault(p => p.Name == "KEYS")?.GetValue(luaArgs) as string[];
            var argv = props.FirstOrDefault(p => p.Name == "ARGV")?.GetValue(luaArgs) as string[];

            Assert.NotNull(argv);
            Assert.NotNull(keys);
            Assert.Single(keys);
            Assert.Single(argv);
            Assert.Equal($"chat_user:{session.UserId}:active", keys[0]);
            Assert.Equal(session.SessionId, argv[0]);
        }

        // -----------------------------------------
        // ❌ Exception handling and fault scenarios
        // -----------------------------------------

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task GetSessionAsync_LogsError_WhenExceptionOccurs(Exception ex, string expectedCode, bool isRetryable)
        {
            // Arrange
            _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ThrowsAsync(ex);

            // Act
            var result = await _store.GetSessionAsync("s1", CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.ErrorCode);

            // Verify logging occurred
            _loggerMock.VerifyAnyLogging(ex);
        }

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task GetActiveSessionIdAsync_LogsError_WhenExceptionOccurs(Exception ex, string expectedCode, bool isRetryable)
        {
            // Arrange
            _mockDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ThrowsAsync(ex);

            // Act
            var result = await _store.GetActiveSessionIdAsync("u1", CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.ErrorCode);

            _loggerMock.VerifyAnyLogging(ex);
        }

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task DeleteSessionAsync_LogsError_WhenExceptionOccurs(Exception ex, string expectedCode, bool isRetryable)
        {
            // Arrange
            _mockDb.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ThrowsAsync(ex);

            // Act
            var result = await _store.DeleteSessionAsync(CreateSession(), CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.ErrorCode);

            _loggerMock.VerifyAnyLogging(ex);
        }

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task SaveSessionAsync_LogsError_WhenExceptionOccurs(Exception ex, string expectedCode, bool isRetryable)
        {
            // Arrange
            _mockDb.Setup(db => db.CreateTransaction(null)).Throws(ex);

            // Act
            var result = await _store.SaveSessionAsync(CreateSession(), CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.ErrorCode);

            _loggerMock.VerifyAnyLogging(ex);
        }

        [Fact]
        public async Task SaveSessionAsync_ReturnsFailure_WhenTransactionFailsToCommit()
        {
            // Arrange
            var tranMock = new Mock<ITransaction>();
            _mockDb.Setup(db => db.CreateTransaction(null)).Returns(tranMock.Object);

            tranMock.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(false);

            var session = CreateSession();

            // Act
            var result = await _store.SaveSessionAsync(session, CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("TRANSACTION_FAILED", result.ErrorCode);
            Assert.True(result.IsRetryable);
        }

        // -----------------------------------------
        // 🛠️ Helpers
        // -----------------------------------------

        public static IEnumerable<object[]> ExceptionTestData =>
            new List<object[]>
            {
                new object[] { new RedisException("timeout"), "REDIS_ERROR", false },
                new object[] { new RedisConnectionException(ConnectionFailureType.SocketFailure, "conn fail"), "REDIS_CONNECTION", true },
                new object[] { new RedisServerException("server error"), "REDIS_SERVER_ERROR", false },
                new object[] { new OperationCanceledException(), "CANCELLED", false },
                new object[] { new JsonException("json error"), "JSON_ERROR", false },
                new object[] { new OperationCanceledException(), "CANCELLED", false }
            };

        private ChatSession CreateSession() => new ChatSession
        {
            SessionId = "s1",
            UserId = "u1",
            Messages = new List<ChatMessage>
            {
                new ChatMessage { Role = ChatRole.User, Content = "Hello", Timestamp = DateTime.UtcNow }
            }
        };

        private void SetupLockManagerMock<T>(Result<T> result)
        {
            _lockManagerMock
                .Setup(m => m.RunWithLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<CancellationToken, Task<Result<T>>>>(),
                    It.IsAny<CancellationToken>()
                ))
                .Returns<string, Func<CancellationToken, Task<Result<T>>>, CancellationToken>(
                    async (key, action, ct) => await action(ct)
                );
        }
    }
}
