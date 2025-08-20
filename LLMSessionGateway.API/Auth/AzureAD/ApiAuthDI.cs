using LLMSessionGateway.API.Auth.Authentication.AzureAD.Configs;
using LLMSessionGateway.API.Auth.Authorization;
using LLMSessionGateway.API.Auth.AzureAD.Authorization.Requirements;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace LLMSessionGateway.API.Auth
{
    public static class ApiAuthDI
    {
        public static IServiceCollection AddApiAzureADAuth(
    this IServiceCollection services,
    IConfiguration config)
        {
            ValidateAndAddConfigs(services, config);

            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtValidationConfigs>>((o, jwtOpt) =>
                {
                    var jwt = jwtOpt.Value;

                    o.Authority = jwt.Authority;
                    o.Audience = jwt.Audience;
                    o.RequireHttpsMetadata = jwt.RequireHttpsMetadata;

                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidAudience = jwt.Audience,
                        ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                        NameClaimType = jwt.ClaimNames.Sub
                    };

                    o.Events = new JwtBearerEvents
                    {
                        OnChallenge = async ctx =>
                        {
                            ctx.HandleResponse();
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            var pf = ctx.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                            var pd = pf.CreateProblemDetails(ctx.HttpContext, StatusCodes.Status401Unauthorized,
                                title: "Unauthorized", detail: "A valid bearer token is required.");
                            await ctx.HttpContext.Response.WriteAsJsonAsync(pd);
                        },
                        OnForbidden = async ctx =>
                        {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            var pf = ctx.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
                            var pd = pf.CreateProblemDetails(ctx.HttpContext, StatusCodes.Status403Forbidden,
                                title: "Forbidden", detail: "You don't have the required scope to access this resource.");
                            await ctx.HttpContext.Response.WriteAsJsonAsync(pd);
                        }
                    };
                })
                .ValidateOnStart(); 

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Scopes.ChatRead, p => p.Requirements.Add(new ScopeRequirement(Scopes.ChatRead)));
                options.AddPolicy(Scopes.ChatSend, p => p.Requirements.Add(new ScopeRequirement(Scopes.ChatSend)));
            });
            services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

            return services;
        }

        private static void ValidateAndAddConfigs(IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<JwtValidationConfigs>()
            .Bind(config.GetSection(JwtValidationConfigs.SectionName))
            .Validate(o => IsValidAzureAuthority(o.Authority),
                "Auth:AzureJwt:Authority must be an absolute HTTPS URL like https://login.microsoftonline.com/{tenant}/v2.0.")
            .Validate(o => NotPlaceholder(o.Authority),
                "Auth:AzureJwt:Authority contains a placeholder; set a real value.")
            .Validate(o => IsValidAzureAudience(o.Audience),
                "Auth:AzureJwt:Audience must be either api://<app-id-uri> or an HTTPS App ID URI, or a GUID client ID.")
            .Validate(o => NotPlaceholder(o.Audience),
                "Auth:AzureJwt:Audience contains a placeholder; set a real value.")
            .Validate(o => o.ClockSkewSeconds is >= 0 and <= 300,
                "Auth:AzureJwt:ClockSkewSeconds must be between 0 and 300 seconds.")
            .Validate(o => o.RequireHttpsMetadata,
                "Auth:AzureJwt:RequireHttpsMetadata must be true for Azure.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClaimNames.Sub),
                "Auth:AzureJwt:ClaimNames:Sub is required (e.g., 'sub').")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClaimNames.Scope),
                "Auth:AzureJwt:ClaimNames:Scope is required (e.g., 'scp').")
            .ValidateOnStart();
        }

        private static bool IsValidAzureAuthority(string? value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
            if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;

            // Accept global & sovereign clouds
            var hostOk =
                uri.Host.EndsWith("microsoftonline.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith("microsoftonline.de", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith("microsoftonline.us", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith("azure.cn", StringComparison.OrdinalIgnoreCase);
            if (!hostOk) return false;

            // Path must be "/{tenant}/v2.0"
            return AzureTenantSegment.IsMatch(uri.AbsolutePath);
        }

        private static bool IsValidAzureAudience(string? aud)
        {
            var s = aud?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return false;

            // 1) GUID client-id
            if (GuidRegex.IsMatch(s)) return true;

            // 2) Absolute URI with allowed schemes
            if (Uri.TryCreate(s, UriKind.Absolute, out var uri))
            {
                var schemeOk = uri.Scheme.Equals("api", StringComparison.OrdinalIgnoreCase) ||
                               uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
                return schemeOk;
            }

            return false;
        }

        private static readonly Regex AzureTenantSegment = new(
            @"^/(?:[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|common|organizations|consumers)/v2\.0/?$",
            RegexOptions.Compiled);

        private static readonly Regex GuidRegex = new(
            @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
            RegexOptions.Compiled);

        private static bool NotPlaceholder(string s) =>
            !(s.Contains('<') || s.Contains('>'));
    }
}
