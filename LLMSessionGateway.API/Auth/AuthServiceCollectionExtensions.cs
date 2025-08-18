using LLMSessionGateway.API.Auth.Authentication.Configs;
using LLMSessionGateway.API.Auth.Authorization;
using LLMSessionGateway.API.Auth.Authorization.Requirements;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LLMSessionGateway.API.Auth
{
    public static class AuthServiceCollectionExtensions
    {
        public static IServiceCollection AddApiAuthenticationAndAuthorization(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Bind JWT options (Authority, Audience, claim names, etc.)
            var jwtOptions = new JwtValidationConfigs();
            configuration.GetSection(JwtValidationConfigs.SectionName).Bind(jwtOptions);

            // Ensure incoming claim names are not remapped
            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.Authority = jwtOptions.Authority;
                    o.Audience = jwtOptions.Audience;
                    o.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;

                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidAudience = jwtOptions.Audience,
                        ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),

                        NameClaimType = jwtOptions.ClaimNames.Sub
                    };

                    o.Events = new JwtBearerEvents
                    {
                        OnChallenge = async ctx =>
                        {
                            ctx.HandleResponse();
                            var pf = ctx.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                            var pd = pf.CreateProblemDetails(ctx.HttpContext, statusCode: StatusCodes.Status401Unauthorized,
                                title: "Unauthorized", detail: "A valid bearer token is required.");
                            await ctx.HttpContext.Response.WriteAsJsonAsync(pd);
                        },
                        OnForbidden = async ctx =>
                        {
                            var pf = ctx.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                            var pd = pf.CreateProblemDetails(ctx.HttpContext, statusCode: StatusCodes.Status403Forbidden,
                                title: "Forbidden", detail: "You don't have the required scope to access this resource.");
                            await ctx.HttpContext.Response.WriteAsJsonAsync(pd);
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Scopes.ChatRead,
                    p => p.Requirements.Add(new ScopeRequirement(Scopes.ChatRead)));
                options.AddPolicy(Scopes.ChatSend,
                    p => p.Requirements.Add(new ScopeRequirement(Scopes.ChatSend)));
            });

            services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

            return services;
        }
    }
}
