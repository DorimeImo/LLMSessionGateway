using FluentAssertions;
using LLMSessionGateway.API.Auth;
using LLMSessionGateway.API.Auth.Authentication.Configs;
using LLMSessionGateway.API.Auth.Authorization;
using LLMSessionGateway.API.Auth.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
{
    public class AuthPolicySliceTests
    {
        private static ServiceProvider BuildProvider()
        {
            var inMem = new Dictionary<string, string?>
            {
                [$"{JwtValidationConfigs.SectionName}:Authority"] = "https://login.microsoftonline.com/dummy/v2.0",
                [$"{JwtValidationConfigs.SectionName}:Audience"] = "api://llmsessiongateway-api",
                // Safer for slice tests (prevents any metadata fetch if auth accidentally runs)
                [$"{JwtValidationConfigs.SectionName}:RequireHttpsMetadata"] = "false",
                [$"{JwtValidationConfigs.SectionName}:ClaimNames:Scope"] = "scp",
                [$"{JwtValidationConfigs.SectionName}:ClaimNames:Sub"] = "sub"
            };

            var cfg = new ConfigurationBuilder().AddInMemoryCollection(inMem).Build();

            var services = new ServiceCollection();

            // ✅ Needed so DefaultAuthorizationService can resolve ILogger<T>
            services.AddLogging();

            // Used by JwtBearer events registered in your auth extension
            services.AddProblemDetails();

            // Your auth/authorization wiring (policies + handler)
            services.AddApiAuthenticationAndAuthorization(cfg);

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
