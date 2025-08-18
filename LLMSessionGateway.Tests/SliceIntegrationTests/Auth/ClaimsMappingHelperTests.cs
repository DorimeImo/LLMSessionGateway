using FluentAssertions;
using LLMSessionGateway.API.Auth.Authentication.Configs;
using LLMSessionGateway.API.Auth.Authorization.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
{
    public class ClaimsMappingHelperTests
    {
        [Fact]
        public void GetScopes_ParsesScpClaim_SpaceDelimited()
        {
            // Arrange
            var claims = new[]
            {
                new Claim("scp", "chat.read chat.send")
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));
            var cn = new ClaimNamesConfigs { Scope = "scp", Sub = "sub" };

            // Act
            var scopes = ClaimsMappingHelper.GetScopes(principal, cn);

            // Assert
            scopes.Should().BeEquivalentTo(new[] { "chat.read", "chat.send" });
        }

        [Fact]
        public void GetScopes_NoScp_ReturnsEmpty()
        {
            // Arrange
            var principal = new ClaimsPrincipal(new ClaimsIdentity());
            var cn = new ClaimNamesConfigs(); // defaults to scp

            // Act
            var scopes = ClaimsMappingHelper.GetScopes(principal, cn);

            // Assert
            scopes.Should().BeEmpty();
        }
    }
}
