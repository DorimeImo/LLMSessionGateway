using LLMSessionGateway.API;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Tests.SliceIntegrationTests.Controller.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Observability.Shared.Contracts;
using System.Text;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Controller
{
    public class ChatFlowControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public ChatFlowControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task FullChatFlow_WorksWithMocks()
        {
            Environment.SetEnvironmentVariable(
                "APPLICATIONINSIGHTS_CONNECTION_STRING",
                "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
                "IngestionEndpoint=https://westeurope-1.in.applicationinsights.azure.com/");

            // Arrange
            var sessionManagerMock = CreateSessionManagerMock();
            var loggerMock = IntegrationTestHelpers.CreateLoggerMock();
            var tracingMock = IntegrationTestHelpers.CreateTracingMock();
            var sendBody = new { message = "hello", messageId = "m1" };

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((ctx, cfg) =>
                {
                    cfg.AddInMemoryCollection(InMemoryConfigurations.CreateInMemoryConfigurations());
                });

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChatSessionOrchestrator>();
                    services.RemoveAll<IStructuredLogger>();
                    services.RemoveAll<ITracingService>();
                    services.RemoveAll<IChatBackend>();

                    services.AddScoped(_ => sessionManagerMock.Object);
                    services.AddScoped(_ => loggerMock.Object);
                    services.AddScoped(_ => tracingMock.Object);

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAzureAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAzureAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAzureAuthHandler>(TestAzureAuthHandler.SchemeName, _ => { });

                    services.AddAuthorization();

                    services.PostConfigure<AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    });
                });
            }).CreateClient();

            // Act
            var startResponse = await client.PostAsync("/api/v1/chat/start", null);
            var sendContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(sendBody),
                Encoding.UTF8,
                "application/json");
            var sendResponse = await client.PostAsync("/api/v1/chat/send?sessionId=abc", sendContent);
            var streamResponse = await client.GetAsync("/api/v1/chat/stream?sessionId=abc&parentMessageId=m1");
            var endResponse = await client.PostAsync("/api/v1/chat/end?sessionId=abc", null);

            // Assert
            await IntegrationTestHelpers.AssertResponseSuccess(startResponse, "StartSession");
            await IntegrationTestHelpers.AssertResponseSuccess(sendResponse, "SendMessage");
            await IntegrationTestHelpers.AssertResponseSuccess(streamResponse, "StreamReply");
            await IntegrationTestHelpers.AssertResponseSuccess(endResponse, "EndSession");

            sessionManagerMock.Verify(m => m.StartSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            sessionManagerMock.Verify(m => m.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            sessionManagerMock.Verify(m => m.StreamReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            sessionManagerMock.Verify(m => m.EndSessionAsync(It.IsAny<string>()), Times.Once);
        }

        private static Mock<IChatSessionOrchestrator> CreateSessionManagerMock()
        {
            var mock = new Mock<IChatSessionOrchestrator>();
            var fakeSession = new ChatSession { SessionId = "abc", UserId = "user" };

            mock.Setup(m => m.StartSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(fakeSession));

            mock.Setup(m => m.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            mock.Setup(m => m.StreamReplyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(IntegrationTestHelpers.FakeStream());

            mock.Setup(m => m.EndSessionAsync(It.IsAny<string>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            return mock;
        }
    }
}