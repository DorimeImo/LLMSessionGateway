using LLMSessionGateway.API.Auth.Authentication.Configs;
using LLMSessionGateway.API.Auth.Authorization;
using LLMSessionGateway.API.Auth.Authorization.Requirements;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace LLMSessionGateway.API.Auth
{
    public static class AuthServiceCollectionExtensions
    {
        public static IServiceCollection AddApiAuthenticationAndAuthorization(
            this IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<JwtValidationConfigs>()
                .Bind(config.GetSection(JwtValidationConfigs.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Authority), "Authority is required")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Audience is required")
                .Validate(o => o.ClockSkewSeconds >= 0, "ClockSkewSeconds must be >= 0")
                .Validate(o => o.Authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
                          "Authority must be HTTPS in production")
                .ValidateOnStart();

            var jwt = config.GetSection(JwtValidationConfigs.SectionName).Get<JwtValidationConfigs>()
                      ?? throw new InvalidOperationException("Auth:Jwt config missing.");

            services.AddProblemDetails();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.Authority = jwt.Authority.TrimEnd('/');
                    o.Audience = jwt.Audience;
                    o.RequireHttpsMetadata = true;
                    o.MapInboundClaims = false;
                    o.RefreshOnIssuerKeyNotFound = true;

                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        RequireSignedTokens = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                        ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
                    };

                    o.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = ctx =>
                        {
                            if (jwt.RequireSub && ctx.Principal?.FindFirst(jwt.ClaimNames.Sub) is null)
                                ctx.Fail("Missing 'sub' claim.");
                            return Task.CompletedTask;
                        },

                        OnChallenge = async ctx =>
                        {
                            ctx.HandleResponse();

                            var problems = ctx.HttpContext.RequestServices
                                .GetRequiredService<IProblemDetailsService>();

                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await problems.WriteAsync(new ProblemDetailsContext
                            {
                                HttpContext = ctx.HttpContext,
                                ProblemDetails = new ProblemDetails
                                {
                                    Title = "Unauthorized",
                                    Status = StatusCodes.Status401Unauthorized,
                                    Detail = "Missing or invalid access token.",
                                    Instance = ctx.HttpContext.Request.Path
                                }.WithExtension("errorCode", "AUTH_401")
                            });
                        },

                        OnForbidden = async ctx =>
                        {
                            var problems = ctx.HttpContext.RequestServices
                                .GetRequiredService<IProblemDetailsService>();

                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            await problems.WriteAsync(new ProblemDetailsContext
                            {
                                HttpContext = ctx.HttpContext,
                                ProblemDetails = new ProblemDetails
                                {
                                    Title = "Forbidden",
                                    Status = StatusCodes.Status403Forbidden,
                                    Detail = "Insufficient scope for this resource.",
                                    Instance = ctx.HttpContext.Request.Path
                                }.WithExtension("errorCode", "AUTH_403")
                            });
                        }
                    };
                });

            services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Scopes.ChatRead,
                    p => p.Requirements.Add(new ScopeRequirement(Scopes.ChatRead)));
                options.AddPolicy(Scopes.ChatSend,
                    p => p.Requirements.Add(new ScopeRequirement(Scopes.ChatSend)));
            });

            return services;
        }

        private static T WithExtension<T>(this T pd, string key, object value) where T : ProblemDetails
        {
            pd.Extensions[key] = value;
            return pd;
        }
    }
}
