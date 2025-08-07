
using LLMSessionGateway.API;
using LLMSessionGateway.Application.Services;
using LLMSessionGateway.Core;
using LLMSessionGateway.Core.Utilities.Functional;
using LLMSessionGateway.Tests.SliceIntegrationTests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Observability.Shared.Contracts;
using System.Text;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests
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
            // Arrange
            var sessionManagerMock = CreateSessionManagerMock();
            var loggerMock = IntegrationTestHelpers.CreateLoggerMock();
            var tracingMock = IntegrationTestHelpers.CreateTracingMock();

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IChatSessionOrchestrator>();
                    services.RemoveAll<IStructuredLogger>();
                    services.RemoveAll<ITracingService>();

                    services.AddScoped(_ => sessionManagerMock.Object);
                    services.AddScoped(_ => loggerMock.Object);
                    services.AddScoped(_ => tracingMock.Object);

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

            sessionManagerMock.Verify(m => m.StartSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            sessionManagerMock.Verify(m => m.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            sessionManagerMock.Verify(m => m.StreamReplyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            sessionManagerMock.Verify(m => m.EndSessionAsync(It.IsAny<string>()), Times.Once);
        }

        private static Mock<IChatSessionOrchestrator> CreateSessionManagerMock()
        {
            var mock = new Mock<IChatSessionOrchestrator>();
            var fakeSession = new ChatSession { SessionId = "abc", UserId = "user" };

            mock.Setup(m => m.StartSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<ChatSession>.Success(fakeSession));

            mock.Setup(m => m.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            mock.Setup(m => m.StreamReplyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(IntegrationTestHelpers.FakeStream());

            mock.Setup(m => m.EndSessionAsync(It.IsAny<string>()))
                .ReturnsAsync(Result<Unit>.Success(Unit.Value));

            return mock;
        }
    }
}