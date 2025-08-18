using LLMSessionGateway.API;
using LLMSessionGateway.API.Auth.Authorization;
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

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
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
                var auth = new Dictionary<string, string?>
                {
                    // Use our local test issuer/audience
                    ["Auth:Jwt:Authority"] = TestTokens.Issuer,
                    ["Auth:Jwt:Audience"] = TestTokens.Audience,
                    ["Auth:Jwt:ClockSkewSeconds"] = "0",
                    ["Auth:Jwt:RequireSub"] = "true",
                    // Azure AD aligned: scopes live in "scp"
                    ["Auth:Jwt:ClaimNames:Scope"] = TestTokens.ScopeClaim,
                    ["Auth:Jwt:ClaimNames:Sub"] = "sub"
                };

                var infra = new Dictionary<string, string?>
                {
                    // Redis (kept simple for tests)
                    ["Redis:ConnectionString"] = "localhost:6379",
                    ["Redis:LockTtlSeconds"] = "30",
                    ["Redis:ActiveSessionTtlSeconds"] = "3600",

                    // Azure Blob (Dev path uses ConnectionString / Azurite)
                    ["AzureBlob:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["AzureBlob:ContainerName"] = "test",

                    // gRPC backend options
                    ["Grpc:ChatService:Host"] = "localhost",
                    ["Grpc:ChatService:Port"] = "5005",
                    ["Grpc:ChatService:UseTls"] = "false",
                    ["Grpc:ChatService:EnableMtls"] = "false",
                    // NEW: validated in config
                    ["Grpc:ChatService:Scope"] = "api://test-backend/.default",

                    // gRPC timeouts/sizes
                    ["Grpc:Timeouts:OpenSeconds"] = "5",
                    ["Grpc:Timeouts:SendSeconds"] = "10",
                    ["Grpc:Timeouts:StreamSetupSeconds"] = "10",
                    ["Grpc:Timeouts:CloseSeconds"] = "5",
                    ["Grpc:Timeouts:MaxSendBytes"] = "4194304",
                    ["Grpc:Timeouts:MaxReceiveBytes"] = "33554432",

                    // Jaeger (only if you read it in Dev)
                    ["OpenTelemetry:Jaeger:AgentHost"] = "localhost",
                    ["OpenTelemetry:Jaeger:AgentPort"] = "6831"
                };

                cfg.AddInMemoryCollection(auth.Concat(infra));
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
