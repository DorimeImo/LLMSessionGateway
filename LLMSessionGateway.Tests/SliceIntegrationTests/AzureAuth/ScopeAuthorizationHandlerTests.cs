using FluentAssertions;
using LLMSessionGateway.API.Auth.Authentication.AzureAD.Configs;
using LLMSessionGateway.API.Auth.Authorization;
using LLMSessionGateway.API.Auth.AzureAD.Authentication.Configs;
using LLMSessionGateway.API.Auth.AzureAD.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.AzureAuth
{
    public class ScopeAuthorizationHandlerTests
    {
        private static ScopeAuthorizationHandler CreateHandler()
        {
            var jwtCfg = new JwtValidationConfigs
            {
                ClaimNames = new ClaimNames { Scope = "scp", Sub = "sub" }
            };
            var opts = Mock.Of<IOptions<JwtValidationConfigs>>(o => o.Value == jwtCfg);
            return new ScopeAuthorizationHandler(opts);
        }

        [Fact]
        public async Task HandleRequirementAsync_HasRequiredScope_Succeeds()
        {
            // Arrange
            var handler = CreateHandler();
            var claims = new[] { new Claim("scp", "chat.read chat.send") };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
            var requirement = new ScopeRequirement("chat.send");
            var ctx = new AuthorizationHandlerContext(new[] { requirement }, user, null);

            // Act
            await handler.HandleAsync(ctx);

            // Assert
            ctx.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task HandleRequirementAsync_MissingScope_DoesNotSucceed()
        {
            // Arrange
            var handler = CreateHandler();
            var claims = new[] { new Claim("scp", "chat.read") };
            var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
            var requirement = new ScopeRequirement("chat.send");
            var ctx = new AuthorizationHandlerContext(new[] { requirement }, user, null);

            // Act
            await handler.HandleAsync(ctx);

            // Assert
            ctx.HasSucceeded.Should().BeFalse();
        }
    }
}
