using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Controller.Helpers
{
    public sealed class TestAzureAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";              // default auth scheme in tests
        public const string ScopesHeader = "X-Test-Scopes";   // space-delimited scopes
        public const string SubHeader = "X-Test-Sub";      // optional subject override

        public TestAzureAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Defaults (can be overridden per request via headers)
            var scopes = Request.Headers[ScopesHeader].ToString();
            if (string.IsNullOrWhiteSpace(scopes))
                scopes = "chat.read chat.send"; // default for convenience

            var sub = Request.Headers[SubHeader].ToString();
            if (string.IsNullOrWhiteSpace(sub))
                sub = "user-123";

            var claims = new[]
            {
            new Claim("sub", sub),
            new Claim("iss", "https://test-issuer"),
            new Claim("scp", scopes) // <-- Azure AD-style scopes
        };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
