using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
{
    public static class TestTokens
    {
        // Static RSA key for the whole test run
        private static readonly RsaSecurityKey _key = CreateKey();

        public static string Issuer { get; } = "https://test-issuer";
        public static string Audience { get; } = "api://llmsessiongateway-api";
        public static string ScopeClaim => "scp";
        public static SecurityKey SecurityKey => _key;

        public static string CreateJwt(string scopesSpaceDelimited, TimeSpan? lifetime = null)
        {
            var now = DateTimeOffset.UtcNow;
            var creds = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256);

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: new[]
                {
                new Claim(ScopeClaim, scopesSpaceDelimited),
                new Claim("sub", Guid.NewGuid().ToString())
                },
                notBefore: now.UtcDateTime,
                expires: (now + (lifetime ?? TimeSpan.FromHours(1))).UtcDateTime,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static RsaSecurityKey CreateKey()
        {
            using var rsa = System.Security.Cryptography.RSA.Create(2048);
            return new RsaSecurityKey(rsa.ExportParameters(true))
            {
                KeyId = Guid.NewGuid().ToString("N")
            };
        }
    }
}
