using FluentAssertions;
using LLMSessionGateway.API.Auth;
using LLMSessionGateway.API.Auth.Authentication.AzureAD.Configs;
using LLMSessionGateway.API.Auth.Authorization;
using LLMSessionGateway.API.Auth.AzureAD.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.AzureAuth
{
    public class AuthPolicySliceTests
    {
        private static ServiceProvider BuildProvider()
        {
            var inMem = InMemoryConfigurations.CreateInMemoryConfigurations();

            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMem).Build();

            var services = new ServiceCollection();

            // Needed so DefaultAuthorizationService can resolve ILogger<T>
            services.AddLogging();

            // Used by JwtBearer events registered in your auth extension
            services.AddProblemDetails();

            // Your auth/authorization wiring (policies + handler)
            services.AddApiAzureADAuth(cfg);

            // (Redundant, but harmless) explicitly ensure the scope handler is present
            services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

            return services.BuildServiceProvider(validateScopes: true);
        }

        [Fact]
        public async Task Policy_AllowsPrincipal_WithRequiredScope()
        {
            using var sp = BuildProvider();
            var authz = sp.GetRequiredService<IAuthorizationService>();

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("scp", "chat.read chat.send"),
                new Claim("sub", "abc")
            }, "jwt"));

            var requirement = new ScopeRequirement("chat.send");
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(requirement)
                .Build();

            var result = await authz.AuthorizeAsync(user, resource: null, policy);

            result.Succeeded.Should().BeTrue();
        }

        [Fact]
        public async Task Policy_DeniesPrincipal_WithoutRequiredScope()
        {
            using var sp = BuildProvider();
            var authz = sp.GetRequiredService<IAuthorizationService>();

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("scp", "chat.read"),
                new Claim("sub", "abc")
            }, "jwt"));

            var requirement = new ScopeRequirement("chat.send");
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(requirement)
                .Build();

            var result = await authz.AuthorizeAsync(user, resource: null, policy);

            result.Succeeded.Should().BeFalse();
        }
    }
}
