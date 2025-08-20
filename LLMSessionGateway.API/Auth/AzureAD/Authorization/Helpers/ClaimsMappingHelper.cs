using LLMSessionGateway.API.Auth.AzureAD.Authentication.Configs;
using System.Security.Claims;
using System.Text.Json;

namespace LLMSessionGateway.API.Auth.AzureAD.Authorization.Helpers
{
    public static class ClaimsMappingHelper
    {
        public static ISet<string> GetScopes(ClaimsPrincipal user, ClaimNames claimNames)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var scopeClaimName = claimNames.Scope;
            var scopeValue = user.FindFirst(scopeClaimName)?.Value;

            if (!string.IsNullOrWhiteSpace(scopeValue))
            {
                foreach (var s in scopeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    set.Add(s);
            }

            return set;
        }
    }
}