using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Security.Claims;
using System.Security.Cryptography;

namespace LLMSessionGateway.API.Auth.Helpers
{
    public static class SubIssUserPartitionKeyHelper
    {
        public static string GetUserIdOrThrow(ClaimsPrincipal user)
        {
            var sub = user.FindFirstValue("sub");
            if (string.IsNullOrEmpty(sub))
                throw new InvalidOperationException("Unauthorized request: missing 'sub' claim.");

            var issuer = user.FindFirstValue("iss");
            if (string.IsNullOrEmpty(issuer))
                throw new InvalidOperationException("Unauthorized request: missing 'iss' claim.");


            var json = System.Text.Json.JsonSerializer.Serialize(new { issuer, sub });
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json));

            return WebEncoders.Base64UrlEncode(hash);
        }
    }
}
