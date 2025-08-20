using FluentAssertions;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Infrastructure.ActiveSessionStore.Redis;
using Moq;
using Observability.Shared.Contracts;
using StackExchange.Redis;
using Xunit;

namespace LLMSessionGateway.Tests.UnitTests.Infrastructure.Redis
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

        [Fact]
        public async Task RunWithLockAsync_ShouldExecuteAction_AndReleaseLock()
        {
            // Arrange
            var lockKey = "chat_lock:user1";

            _mockRedisDb
                .Setup(db => db.StringSetAsync(lockKey, It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), When.NotExists))
                .ReturnsAsync(true);

            _mockRedisDb
                .Setup(db => db.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), CommandFlags.None))
                .ReturnsAsync(RedisResult.Create(1));

            // Act
            var result = await _lockManager.RunWithLockAsync(lockKey, async ct =>
            {
                await Task.Delay(10, ct);
                return Result<Unit>.Success(Unit.Value);
            });

            // Assert
            result.IsSuccess.Should().BeTrue();
            _mockRedisDb.Verify(db => db.StringSetAsync(lockKey, It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), When.NotExists), Times.Once);
            _mockRedisDb.Verify(db => db.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), CommandFlags.None), Times.Once);
        }

        [Fact]
        public async Task RunWithLockAsync_ShouldReturnFailure_WhenLockNotAcquired()
        {
            // Arrange
            _mockRedisDb
                .Setup(db => db.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), When.NotExists))
                .ReturnsAsync(false);

            // Act
            var result = await _lockManager.RunWithLockAsync("lock_key", async ct => Result<Unit>.Success(Unit.Value));

            // Assert
            result.IsFailure.Should().BeTrue();
            result.ErrorCode.Should().Be("REDIS_LOCK_FAILED");
        }

        [Fact]
        public async Task RunWithLockAsync_ShouldReleaseLock_EvenIfActionThrows()
        {
            // Arrange
            var lockKey = "chat_lock:user2";

            _mockRedisDb
                .Setup(db => db.StringSetAsync(lockKey, It.IsAny<RedisValue>(), It.IsAny<TimeSpan>(), When.NotExists))
                .ReturnsAsync(true);

            _mockRedisDb
                .Setup(db => db.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), CommandFlags.None))
                .ReturnsAsync(RedisResult.Create(1));

            // Act
            // Act
            var result = await _lockManager.RunWithLockAsync<string>(lockKey, ct =>
            {
                throw new RedisException("Failing");
            }, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            _mockRedisDb.Verify(db => db.ScriptEvaluateAsync(It.IsAny<LuaScript>(), It.IsAny<object>(), CommandFlags.None), Times.Once);
        }
    }

}
