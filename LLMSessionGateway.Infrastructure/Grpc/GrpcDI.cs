using Grpc.Core;
using Grpc.Net.Client.Configuration;
using LLMSessionGateway.Application.Contracts.Ports;
using LLMSessionGateway.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            services.Configure<GrpcConfigs>(config.GetSection("Grpc:ChatService"));
            services.Configure<GrpcTimeoutsConfigs>(config.GetSection("Grpc:Timeouts"));

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
