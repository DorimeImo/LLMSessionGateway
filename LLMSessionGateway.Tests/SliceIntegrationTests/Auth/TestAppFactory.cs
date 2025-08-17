using LLMSessionGateway.API;
using LLMSessionGateway.API.Auth;
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

namespace LLMSessionGateway.Tests.SliceIntegrationTests.Auth
{
    public class TestAppFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateOnBuild = false;
                o.ValidateScopes = false;
            });

            builder.ConfigureAppConfiguration((ctx, cfg) =>
            {
                var auth = new Dictionary<string, string?>
                {
                    ["Auth:Jwt:Authority"] = TestTokens.Issuer,
                    ["Auth:Jwt:Audience"] = TestTokens.Audience,
                    ["Auth:Jwt:ClockSkewSeconds"] = "30",
                    ["Auth:Jwt:RequireSub"] = "true",
                    ["Auth:Jwt:ClaimNames:Scope"] = TestTokens.ScopeClaim,
                    ["Auth:Jwt:ClaimNames:Sub"] = "sub"
                };

                var infra = new Dictionary<string, string?>
                {
                    // Redis
                    ["Redis:ConnectionString"] = "localhost:6379",
                    ["Redis:LockTtlSeconds"] = "30",
                    ["Redis:ActiveSessionTtlSeconds"] = "3600",

                    // Azure Blob
                    ["AzureBlob:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["AzureBlob:ContainerName"] = "test",

                    // gRPC backend options (new schema)
                    ["Grpc:ChatService:Host"] = "localhost",
                    ["Grpc:ChatService:Port"] = "5005",
                    ["Grpc:ChatService:UseTls"] = "true",
                    ["Grpc:ChatService:EnableMtls"] = "false",
                    // If you enable mTLS, also set these and provide real env vars:
                    // ["Grpc:ChatService:ClientCertificateBase64Env"] = "PFX_B64",
                    // ["Grpc:ChatService:ClientCertificatePasswordEnv"] = "PFX_PWD",

                    // gRPC per-call timeouts/sizes (seconds + bytes)
                    ["Grpc:Timeouts:OpenSeconds"] = "5",
                    ["Grpc:Timeouts:SendSeconds"] = "10",
                    ["Grpc:Timeouts:StreamSetupSeconds"] = "10",
                    ["Grpc:Timeouts:CloseSeconds"] = "5",
                    ["Grpc:Timeouts:MaxSendBytes"] = "4194304",
                    ["Grpc:Timeouts:MaxReceiveBytes"] = "33554432",

                    // Logging (if your Serilog options are validated)
                    ["Logging:File:BasePath"] = "Logs",
                    ["Logging:File:FileNamePattern"] = "log-.txt",
                    ["Logging:File:RollingInterval"] = "Day",

                    // OpenTelemetry (if validated)
                    ["OpenTelemetry:Jaeger:AgentHost"] = "localhost",
                    ["OpenTelemetry:Jaeger:AgentPort"] = "6831"
                };

                // Add both
                cfg.AddInMemoryCollection(auth.Concat(infra));
            });

            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                {
                    var staticOidc =
                        new StaticOpenIdConfig.StaticConfigManager<OpenIdConnectConfiguration>(StaticOpenIdConfig.Oidc);
                    o.ConfigurationManager = staticOidc;
                    o.RequireHttpsMetadata = false;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = TestTokens.Issuer,
                        ValidAudience = TestTokens.Audience,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 }
                    };
                });

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
