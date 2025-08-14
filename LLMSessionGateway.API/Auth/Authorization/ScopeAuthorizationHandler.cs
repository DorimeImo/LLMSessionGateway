using LLMSessionGateway.API.Auth.Authentication.Configs;
using LLMSessionGateway.API.Auth.Authorization.Helpers;
using LLMSessionGateway.API.Auth.Authorization.Requirements;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace LLMSessionGateway.API.Auth.Authorization
{
    public sealed class ScopeAuthorizationHandler : AuthorizationHandler<ScopeRequirement>
    {
        private readonly ClaimNamesConfigs _claimNames;

        public ScopeAuthorizationHandler(IOptions<JwtValidationConfigs> jwtOptions)
        {
            _claimNames = jwtOptions.Value.ClaimNames ?? new ClaimNamesConfigs();
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext ctx, ScopeRequirement req)
        {
            var scopes = ClaimsMappingHelper.GetScopes(ctx.User, _claimNames);
            if (scopes.Contains(req.Scope))
            {
                ctx.Succeed(req);
            }
            return Task.CompletedTask;
        }
    }
}
