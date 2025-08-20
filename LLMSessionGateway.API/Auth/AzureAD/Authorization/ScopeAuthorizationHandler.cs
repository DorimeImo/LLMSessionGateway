
using LLMSessionGateway.API.Auth.Authentication.AzureAD.Configs;
using LLMSessionGateway.API.Auth.AzureAD.Authentication.Configs;
using LLMSessionGateway.API.Auth.AzureAD.Authorization.Helpers;
using LLMSessionGateway.API.Auth.AzureAD.Authorization.Requirements;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace LLMSessionGateway.API.Auth.Authorization
{
    public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
    {
        private readonly ClaimNames _claimNames;

        public ScopeAuthorizationHandler(IOptions<JwtValidationConfigs> jwtOptions)
        {
            _claimNames = jwtOptions.Value.ClaimNames ?? new ClaimNames();
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
        {
            var scopes = ClaimsMappingHelper.GetScopes(context.User, _claimNames);
            if (scopes.Contains(requirement.Scope))
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }
}
