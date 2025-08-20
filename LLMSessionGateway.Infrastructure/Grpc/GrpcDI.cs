using Grpc.Core;
using Grpc.Net.Client.Configuration;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Infrastructure.ArchiveSessionStore.Redis;
using LLMSessionGateway.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Grpc
{
    public static class GrpcDI
    {
        public static IServiceCollection AddGrpcChatBackend(this IServiceCollection services, IConfiguration config)
        {
            ValidateAndAddConfigs(services, config);

            services.AddGrpcClient<ChatService.ChatServiceClient>((sp, o) =>
            {
                var cfg = sp.GetRequiredService<IOptions<GrpcConfigs>>().Value;
                var scheme = cfg.UseTls ? "https" : "http";
                o.Address = new Uri($"{scheme}://{cfg.Host}:{cfg.Port}");
            })
            .AddCallCredentials(async (ctx, metadata, sp) =>
            {
                var cfg = sp.GetRequiredService<IOptions<GrpcConfigs>>().Value;
                var tokenProvider = sp.GetRequiredService<ITokenProvider>();

                var tokenRes = await tokenProvider.GetTokenAsync(cfg.Scope, ctx.CancellationToken)
                                                  .ConfigureAwait(false);

                if (tokenRes.IsSuccess)
                {
                    metadata.Add("Authorization", $"Bearer {tokenRes.Value}");
                }
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var cfg = sp.GetRequiredService<IOptions<GrpcConfigs>>().Value;

                var handler = new SocketsHttpHandler
                {
                    KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(15),

                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),

                    MaxConnectionsPerServer = 32,
                    AutomaticDecompression = DecompressionMethods.None,
                    ConnectTimeout = TimeSpan.FromSeconds(5),
                };

                if (cfg.UseTls)
                {
                    handler.SslOptions = new SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
                        CertificateRevocationCheckMode = X509RevocationMode.Online
                    };
                }

                if (cfg.EnableMtls)
                {
                    var cert = LoadClientCertificateFromEnv(cfg)
                        ?? throw new InvalidOperationException("mTLS is enabled but client certificate is missing.");
                    handler.SslOptions.ClientCertificates = new X509CertificateCollection { cert };
                }

                return handler;
            })
            .ConfigureChannel((sp, ch) =>
            {
                var to = sp.GetRequiredService<IOptions<GrpcTimeoutsConfigs>>().Value;

                ch.MaxSendMessageSize = to.MaxSendBytes;
                ch.MaxReceiveMessageSize = to.MaxReceiveBytes;
            });

            services.AddScoped<IChatBackend, GrpcChatBackend>();
            return services;
        }

        private static void ValidateAndAddConfigs(IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<GrpcConfigs>()
                .Bind(config.GetSection("Grpc:ChatService"))
                .ValidateDataAnnotations()
                .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Grpc:ChatService:Host is required.")
                .Validate(o => o.Port is > 0 and <= 65535, "Grpc:ChatService:Port must be 1..65535.")
                .Validate<IHostEnvironment>(
                    (o, env) => env.IsDevelopment() || o.UseTls,
                    "Grpc:ChatService:UseTls must be true outside Development.")
                .Validate(o =>
                    !o.EnableMtls ||
                    (!string.IsNullOrWhiteSpace(o.ClientCertificateBase64Env)
                     && !string.IsNullOrWhiteSpace(o.ClientCertificatePasswordEnv)),
                    "When EnableMtls=true, set ClientCertificateBase64Env and ClientCertificatePasswordEnv.")
                .Validate(o => IsValidScope(o.Scope),
                    "Grpc:ChatService:Scope must be an absolute URI like 'api://app/.default' or 'api://app/read'.")
                .ValidateOnStart();

            services.AddOptions<GrpcTimeoutsConfigs>()
                .Bind(config.GetSection("Grpc:Timeouts"))
                .Validate(o => o.OpenSeconds > 0, "Grpc:Timeouts:OpenSeconds must be > 0.")
                .Validate(o => o.SendSeconds > 0, "Grpc:Timeouts:SendSeconds must be > 0.")
                .Validate(o => o.StreamSetupSeconds > 0, "Grpc:Timeouts:StreamSetupSeconds must be > 0.")
                .Validate(o => o.CloseSeconds > 0, "Grpc:Timeouts:CloseSeconds must be > 0.")
                .Validate(o => o.MaxSendBytes > 0 && o.MaxReceiveBytes > 0,
                          "Grpc:Timeouts:MaxSendBytes and MaxReceiveBytes must be > 0.")
                .Validate(o => o.MaxReceiveBytes >= o.MaxSendBytes,
                          "Grpc:Timeouts:MaxReceiveBytes must be >= MaxSendBytes.")
                .ValidateOnStart();
        }

        private static bool IsValidScope(string? scope)
        {
            var s = scope?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return false;

            var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            foreach (var t in tokens)
            {
                if (Uri.TryCreate(t, UriKind.Absolute, out var uri))
                {
                    var schemeOk = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                                   || uri.Scheme.Equals("api", StringComparison.OrdinalIgnoreCase);
                    if (schemeOk && !string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                        continue;

                    return false;
                }

                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[\x21\x23-\x5B\x5D-\x7E]+$"))
                    continue;

                return false;
            }

            return true;
        }

        private static X509Certificate2 LoadClientCertificateFromEnv(GrpcConfigs cfg)
        {
            var pfxB64 = Environment.GetEnvironmentVariable(cfg.ClientCertificateBase64Env!)
                ?? throw new InvalidOperationException($"Env var '{cfg.ClientCertificateBase64Env}' is missing.");
            var pwd = Environment.GetEnvironmentVariable(cfg.ClientCertificatePasswordEnv!)
                ?? throw new InvalidOperationException($"Env var '{cfg.ClientCertificatePasswordEnv}' is missing or empty.");

            var bytes = Convert.FromBase64String(pfxB64);
            return new X509Certificate2(bytes, pwd, X509KeyStorageFlags.EphemeralKeySet);
        }
    }
}
