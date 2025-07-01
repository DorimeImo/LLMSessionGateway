using LLMSessionGateway.Application.Contracts.Logging;
using LLMSessionGateway.Application.Contracts.Observability;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Contracts.Resilience;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Tests.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using System.Text;
using Xunit;

namespace LLMSessionGateway.Tests.IntegrationTests
{
    public class ChatFlowSessionManagerIntegrationTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly ApiWebApplicationFactory _factory;

        public ChatFlowSessionManagerIntegrationTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task FullChatFlow_WorksWithSessionManager()
        {
            // Arrange
            var activeStoreMock = new Mock<IActiveSessionStore>();
            var archiveStoreMock = new Mock<IArchiveSessionStore>();
            var chatBackendMock = new Mock<IChatBackend>();
            var retryPolicyMock = new Mock<IRetryPolicyRunner>();
            var lockManagerMock = new Mock<IDistributedLockManager>();
            var loggerMock = IntegrationTestHelpers.CreateLoggerMock();
            var tracingMock = IntegrationTestHelpers.CreateTracingMock();

            IntegrationTestHelpers.ConfigureActiveSessionStore(activeStoreMock);
            IntegrationTestHelpers.ConfigureArchiveSessionStoreMock(archiveStoreMock);
            IntegrationTestHelpers.ConfigureChatBackendMock(chatBackendMock);
            IntegrationTestHelpers.ConfigureRetryPolicyMock(retryPolicyMock);
            IntegrationTestHelpers.ConfigureLockManagerMock(lockManagerMock);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IConnectionMultiplexer>();
                    services.RemoveAll<IActiveSessionStore>();
                    services.RemoveAll<IArchiveSessionStore>();
                    services.RemoveAll<IChatBackend>();
                    services.RemoveAll<IRetryPolicyRunner>();
                    services.RemoveAll<IDistributedLockManager>();
                    services.RemoveAll<IStructuredLogger>();
                    services.RemoveAll<ITracingService>();

                    services.AddScoped(_ => activeStoreMock.Object);
                    services.AddScoped(_ => archiveStoreMock.Object);
                    services.AddScoped(_ => chatBackendMock.Object);
                    services.AddScoped(_ => retryPolicyMock.Object);
                    services.AddScoped(_ => lockManagerMock.Object);
                    services.AddScoped(_ => loggerMock.Object);
                    services.AddScoped(_ => tracingMock.Object);

                    // Authentication
                    services.AddAuthentication("Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

                    services.PostConfigure<AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    });
                });
            }).CreateClient();

            // Act
            var startResponse = await client.PostAsync("/api/chat/start", null);
            var sendContent = new StringContent("\"hello\"", Encoding.UTF8, "application/json");
            var sendResponse = await client.PostAsync("/api/chat/send?sessionId=abc", sendContent);
            var streamResponse = await client.GetAsync("/api/chat/stream?sessionId=abc");
            var endResponse = await client.PostAsync("/api/chat/end?sessionId=abc", null);

            // Assert
            await IntegrationTestHelpers.AssertResponseSuccess(startResponse, "StartSession");
            await IntegrationTestHelpers.AssertResponseSuccess(sendResponse, "SendMessage");
            await IntegrationTestHelpers.AssertResponseSuccess(streamResponse, "StreamReply");
            await IntegrationTestHelpers.AssertResponseSuccess(endResponse, "EndSession");
        }
    }
}
