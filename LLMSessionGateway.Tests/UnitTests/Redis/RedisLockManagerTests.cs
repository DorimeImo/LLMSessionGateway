
using LLMSessionGateway.Infrastructure.Redis;
using LLMSessionGateway.Tests.UnitTests.Helpers;
using Moq;
using Observability.Shared.Contracts;
using StackExchange.Redis;
using System.Runtime.Serialization;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Redis
{
    public class RedisLockManagerTests
    {
        private readonly Mock<IDatabase> _mockRedisDb;
        private readonly Mock<IStructuredLogger> _loggerMock;
        private readonly RedisLockManager _lockManager;

        public RedisLockManagerTests()
        {
            _mockRedisDb = new Mock<IDatabase>();
            _loggerMock = new Mock<IStructuredLogger>();

            var redis = new Mock<IConnectionMultiplexer>();
            redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                 .Returns(_mockRedisDb.Object);

            _lockManager = new RedisLockManager(
                redis.Object,
                _loggerMock.Object,
                new Mock<ITracingService>().Object,
                TimeSpan.FromSeconds(15)
            );
        }

        // -----------------------------------------
        // ✅ Core success and expected flow tests
        // -----------------------------------------

        [Fact]
        public async Task AcquireLockAsync_SetsLockWithPositiveTtl()
        {
            // Arrange
            _mockRedisDb
                .Setup(db => db.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan>(),
                    When.NotExists))
                .ReturnsAsync(true);

            // Act
            var result = await _lockManager.AcquireLockAsync("chat_lock:user1", CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            _mockRedisDb.Verify(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.Is<TimeSpan>(ttl => ttl.TotalMilliseconds > 0),
                When.NotExists), Times.Once);
        }

        [Fact]
        public async Task AcquireLockAsync_ShouldReturnSuccess_WhenAcquired()
        {
            // Arrange
            _mockRedisDb
                .Setup(db => db.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan>(),
                    When.NotExists))
                .ReturnsAsync(true);

            // Act
            var result = await _lockManager.AcquireLockAsync("chat_lock:user1", CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.False(string.IsNullOrEmpty(result.Value));
        }

        [Fact]
        public async Task ReleaseLockAsync_UsesCorrectLuaScriptWithCorrectArguments()
        {
            // Arrange
            object? capturedArgs = null;

            _mockRedisDb
                .Setup(db => db.ScriptEvaluateAsync(
                    It.IsAny<LuaScript>(),
                    It.IsAny<object>(),
                    CommandFlags.None))
                .Callback<LuaScript, object, CommandFlags>((_, args, _) => capturedArgs = args)
                .ReturnsAsync(RedisResult.Create(1));

            // Act
            var result = await _lockManager.ReleaseLockAsync("chat_lock:user1", "some-lock-value");

            // Assert
            Assert.True(result.IsSuccess);

            var props = capturedArgs?.GetType().GetProperties();
            var keys = props?.FirstOrDefault(p => p.Name == "KEYS")?.GetValue(capturedArgs) as string[];
            var argv = props?.FirstOrDefault(p => p.Name == "ARGV")?.GetValue(capturedArgs) as string[];

            Assert.NotNull(keys);
            Assert.NotNull(argv);
            Assert.Single(keys!);
            Assert.Single(argv!);
            Assert.Equal("chat_lock:user1", keys[0]);
            Assert.Equal("some-lock-value", argv[0]);

            _mockRedisDb.Verify(db => db.ScriptEvaluateAsync(
                It.IsAny<LuaScript>(),
                It.IsAny<object>(),
                CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task ReleaseLockAsync_ShouldReturnSuccess_WhenNoException()
        {
            // Arrange
            _mockRedisDb
                .Setup(db => db.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), CommandFlags.None))
                .ReturnsAsync(RedisResult.Create(1));

            // Act
            var result = await _lockManager.ReleaseLockAsync("key", "value");

            // Assert
            Assert.True(result.IsSuccess);
        }

        // -----------------------------------------
        // ❌ Exception handling and fault scenarios
        // -----------------------------------------

        [Fact]
        public async Task AcquireLockAsync_ShouldReturnFailure_WhenNotAcquired()
        {
            // Arrange
            _mockRedisDb
                .Setup(db => db.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan>(),
                    When.NotExists))
                .ReturnsAsync(false);

            // Act
            var result = await _lockManager.AcquireLockAsync("chat_lock:user1", CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal("REDIS_LOCK_FAILED", result.ErrorCode);
        }

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task AcquireLockAsync_LogsError_WhenExceptionOccurs(Exception ex, string expectedCode, bool isRetryable)
        {
            // Arrange
            _mockRedisDb
                .Setup(db => db.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan>(),
                    When.NotExists))
                .ThrowsAsync(ex);

            // Act
            var result = await _lockManager.AcquireLockAsync("key", CancellationToken.None);

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.ErrorCode);

            _loggerMock.VerifyAnyLogging(ex);
        }

        [Theory]
        [MemberData(nameof(ExceptionTestData))]
        public async Task ReleaseLockAsync_LogsError_WhenExceptionOccurs(Exception ex, string expectedCode, bool isRetryable)
        {
            // Arrange
            _mockRedisDb
                .Setup(db => db.ScriptEvaluateAsync(
                    It.IsAny<LuaScript>(),
                    It.IsAny<object>(),
                    CommandFlags.None))
                .ThrowsAsync(ex);

            // Act
            var result = await _lockManager.ReleaseLockAsync("key", "value");

            // Assert
            Assert.True(result.IsFailure);
            Assert.Equal(expectedCode, result.ErrorCode);

            _loggerMock.VerifyAnyLogging(ex);
        }

        // -----------------------------------------
        // 🛠️ Helpers
        // -----------------------------------------
        public static IEnumerable<object[]> ExceptionTestData => new List<object[]>
        {
            new object[] { new RedisException("timeout"), "REDIS_ERROR", false },
            new object[] { new RedisConnectionException(ConnectionFailureType.UnableToConnect, "conn fail"), "REDIS_CONNECTION", true },
            new object[] { new RedisServerException("server error"), "REDIS_SERVER_ERROR", false },
            new object[] { new OperationCanceledException(), "CANCELLED", false }
        };
    }
}
