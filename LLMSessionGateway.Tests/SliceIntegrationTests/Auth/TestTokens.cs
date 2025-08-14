using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
{
    public static class TestTokens
    {
        public const string Issuer = "https://test-issuer";
        public const string Audience = "llm-session-gateway";
        public const string ScopeClaim = "scope"; // matches your ClaimNamesConfigs

        private static readonly RSA Rsa = RSA.Create(2048);
        public static readonly RsaSecurityKey SigningKey = new(Rsa) { KeyId = "test-key-1" };
        private static readonly SigningCredentials Creds =
            new(SigningKey, SecurityAlgorithms.RsaSha256);

        public static string Create(
            string? subject = "user-123",
            IEnumerable<string>? scopes = null,
            DateTimeOffset? expires = null,
            DateTimeOffset? notBefore = null)
        {
            var now = DateTimeOffset.UtcNow;
            var exp = expires ?? now.AddMinutes(10);

            if (!notBefore.HasValue)
            {
                notBefore = exp <= now ? exp.AddMinutes(-10) : now.AddMinutes(-1);
            }

            if (notBefore > exp)
                notBefore = exp.AddSeconds(-1);

            var claims = new List<Claim>();
            if (!string.IsNullOrWhiteSpace(subject))
                claims.Add(new Claim(JwtRegisteredClaimNames.Sub, subject));
            if (scopes is { })
                claims.Add(new Claim(ScopeClaim, string.Join(' ', scopes)));

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                notBefore: notBefore.Value.UtcDateTime,
                expires: exp.UtcDateTime,
                signingCredentials: Creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
