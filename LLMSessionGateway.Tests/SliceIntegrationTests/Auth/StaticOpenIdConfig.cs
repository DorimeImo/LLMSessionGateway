using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
{
    public static class StaticOpenIdConfig
    {
        public static readonly OpenIdConnectConfiguration Oidc;

        static StaticOpenIdConfig()
        {
            Oidc = new OpenIdConnectConfiguration
            {
                Issuer = TestTokens.Issuer
            };

            // Publish our test RSA key as if it came from JWKS
            Oidc.SigningKeys.Add(TestTokens.SigningKey);
        }

        public sealed class StaticConfigManager<T> : IConfigurationManager<T> where T : class
        {
            private readonly T _config;
            public StaticConfigManager(T config) => _config = config;
            public Task<T> GetConfigurationAsync(CancellationToken cancel) => Task.FromResult(_config);
            public void RequestRefresh() { /* no-op */ }
        }
    }
}
