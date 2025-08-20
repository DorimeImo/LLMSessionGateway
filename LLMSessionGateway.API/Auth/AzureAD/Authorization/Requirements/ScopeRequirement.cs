using Microsoft.AspNetCore.Authorization;

namespace LLMSessionGateway.API.Auth.AzureAD.Authorization.Requirements
{
    public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
    {
        public string Scope { get; } = scope;
    }
}
