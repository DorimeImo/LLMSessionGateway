using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
{
    public class AuthTests : IClassFixture<TestAppFactory>
    {
        private readonly HttpClient _client;

        public AuthTests(TestAppFactory factory)
        {
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        [Fact]
        public async Task NoToken_Returns401_WithProblemJson()
        {
            // Arrange
            var req = new HttpRequestMessage(HttpMethod.Get, "/read");

            // Act
            var res = await _client.SendAsync(req);

            // Assert
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            res.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            var json = await res.Content.ReadAsStringAsync();
            json.Should().Contain("\"errorCode\":\"AUTH_401\"");
            json.Should().Contain("\"title\":\"Unauthorized\"");
        }

        [Fact]
        public async Task ValidToken_MissingScope_Returns403_WithProblemJson()
        {
            // Arrange: token with no chat.read
            var token = TestTokens.Create(scopes: new[] { "profile" });
            var req = new HttpRequestMessage(HttpMethod.Get, "/read");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var res = await _client.SendAsync(req);

            // Assert
            res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            res.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            var json = await res.Content.ReadAsStringAsync();
            json.Should().Contain("\"errorCode\":\"AUTH_403\"");
            json.Should().Contain("\"title\":\"Forbidden\"");
            json.Should().Contain("Insufficient scope");
        }

        [Fact]
        public async Task ValidToken_WithChatReadScope_Returns200()
        {
            // Arrange
            var token = TestTokens.Create(scopes: new[] { "chat.read" });
            var req = new HttpRequestMessage(HttpMethod.Get, "/read");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var res = await _client.SendAsync(req);

            // Assert
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await res.Content.ReadAsStringAsync();
            body.Should().Contain("\"ok\":true");
        }

        [Fact]
        public async Task ValidToken_WithChatSendScope_Returns200_OnSend()
        {
            // Arrange
            var token = TestTokens.Create(scopes: new[] { "chat.send" });
            var req = new HttpRequestMessage(HttpMethod.Post, "/send")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var res = await _client.SendAsync(req);

            // Assert
            res.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task MissingSub_WhenRequired_Returns401()
        {
            // Arrange: subject is null -> hits your `RequireSub` check and fails
            var token = TestTokens.Create(subject: null, scopes: new[] { "chat.read" });
            var req = new HttpRequestMessage(HttpMethod.Get, "/read");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var res = await _client.SendAsync(req);

            // Assert
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var json = await res.Content.ReadAsStringAsync();
            json.Should().Contain("\"errorCode\":\"AUTH_401\"");
            (json.Contains("Missing or invalid access token") || json.Contains("Missing 'sub' claim"))
                .Should().BeTrue();
        }

        [Fact]
        public async Task ExpiredToken_Returns401()
        {
            // Arrange: expired 1 minute ago
            var token = TestTokens.Create(scopes: new[] { "chat.read" }, expires: DateTimeOffset.UtcNow.AddMinutes(-10));
            var req = new HttpRequestMessage(HttpMethod.Get, "/read");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var res = await _client.SendAsync(req);

            // Assert
            res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            var json = await res.Content.ReadAsStringAsync();
            json.Should().Contain("\"errorCode\":\"AUTH_401\"");
        }
    }
}
