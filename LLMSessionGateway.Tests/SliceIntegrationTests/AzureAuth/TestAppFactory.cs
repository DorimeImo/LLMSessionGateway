using LLMSessionGateway.API;
using LLMSessionGateway.API.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.SliceIntegrationTests.AzureAuth
{
    public class TestAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Use Development to avoid prod-only validations in slice tests
            builder.UseEnvironment("Development");

            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateOnBuild = false;
                o.ValidateScopes = false;
            });

            builder.ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.AddInMemoryCollection(InMemoryConfigurations.CreateInMemoryConfigurations());

            });

            builder.ConfigureTestServices(services =>
            {
                // Logging is required by DefaultAuthorizationService
                services.AddLogging();
                services.AddProblemDetails();

                // Make JwtBearer validate tokens against our local RSA key
                JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    // No metadata round-trips in tests
                    o.RequireHttpsMetadata = false;
                    o.Authority = null; // ignore metadata
                    o.Audience = null;

                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = TestTokens.Issuer,
                        ValidAudience = TestTokens.Audience,

                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        IssuerSigningKey = TestTokens.SecurityKey,
                        ClockSkew = TimeSpan.Zero,

                        NameClaimType = "sub",
                        RoleClaimType = "roles"
                    };
                });

                // If your endpoints resolve these, remove or stub them for slice tests
                services.RemoveAll<LLMSessionGateway.Application.Contracts.Ports.IChatBackend>();
                services.RemoveAll<LLMSessionGateway.Application.Services.IChatSessionOrchestrator>();
                services.RemoveAll<LLMSessionGateway.Application.Services.ISessionLifecycleService>();
            });

            builder.Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/read", () => Results.Ok(new { ok = true }))
                             .RequireAuthorization(Scopes.ChatRead);

                    endpoints.MapPost("/send", () => Results.Ok(new { ok = true }))
                             .RequireAuthorization(Scopes.ChatSend);
                });
            });
        }
    }
}
