using Grpc.Core;
using Grpc.Net.Client.Configuration;
using LLMSessionGateway.Application.Contracts.Ports;
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
                if (!cfg.UseTls)
                    throw new InvalidOperationException("TLS must be enabled for gRPC in production.");

                o.Address = new Uri($"https://{cfg.Host}:{cfg.Port}");
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var cfg = sp.GetRequiredService<IOptions<GrpcConfigs>>().Value;

                var handler = new SocketsHttpHandler
                {
                    // HTTP/2 + keepalives for gRPC
                    KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(15),

                    // Refresh pooled connections periodically to ride over DNS/LB changes
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),

                    MaxConnectionsPerServer = 32,
                    AutomaticDecompression = DecompressionMethods.None,
                    ConnectTimeout = TimeSpan.FromSeconds(5),

                    SslOptions = new SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
                        CertificateRevocationCheckMode = X509RevocationMode.Online
                    }
                };

                // mTLS (optional)
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

                // Global defaults; we’ll fine-tune per method (see below in your client class)
                var retry = new RetryPolicy
                {
                    MaxAttempts = 4,
                    InitialBackoff = TimeSpan.FromMilliseconds(200),
                    MaxBackoff = TimeSpan.FromSeconds(2),
                    BackoffMultiplier = 2.0,
                    RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.DeadlineExceeded }
                };

                ch.ServiceConfig = new ServiceConfig
                {
                    MethodConfigs =
                    {
                    new MethodConfig
                    {
                        Names = { MethodName.Default },
                        RetryPolicy = retry
                    }
                    }
                    // Load-balancing isn’t needed; ACA ingress load-balances for you.
                };
            });

            services.AddScoped<IChatBackend, GrpcChatBackend>();
            return services;
        }

        private static X509Certificate2? LoadClientCertificateFromEnv(GrpcConfigs cfg)
        {
            if (string.IsNullOrWhiteSpace(cfg.ClientCertificateBase64Env))
                return null;

            var pfxB64 = Environment.GetEnvironmentVariable(cfg.ClientCertificateBase64Env);
            if (string.IsNullOrWhiteSpace(pfxB64))
                throw new InvalidOperationException($"Env var '{cfg.ClientCertificateBase64Env}' is empty.");

            var pwd = string.IsNullOrWhiteSpace(cfg.ClientCertificatePasswordEnv)
                ? null
                : Environment.GetEnvironmentVariable(cfg.ClientCertificatePasswordEnv);

            var bytes = Convert.FromBase64String(pfxB64);
            // If your PFX in KV has a password, pass it; otherwise this can be null or empty.
            return string.IsNullOrEmpty(pwd)
                ? new X509Certificate2(bytes)
                : new X509Certificate2(bytes, pwd);
        }
    }
}
