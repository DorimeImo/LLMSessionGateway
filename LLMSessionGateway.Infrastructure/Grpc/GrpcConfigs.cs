using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Grpc
{
    public sealed class GrpcConfigs
    {
        public string Host { get; init; } = "chat-backend.internal.azurecontainerapps.io";
        public int Port { get; init; } = 443;
        public bool UseTls { get; init; } = true;
        public bool EnableMtls { get; init; } = false;
        public string? ClientCertificateBase64Env { get; init; }
        public string? ClientCertificatePasswordEnv { get; init; }

        /// <summary>
        /// Full OAuth2 scope for the downstream gRPC backend
        /// (e.g. "api://chat-backend/.default" or "api://chat-backend/send")
        /// </summary>
        public string Scope { get; init; } = string.Empty;
    }

    public sealed class GrpcTimeoutsConfigs
    {
        public int OpenSeconds { get; init; } = 10;
        public int SendSeconds { get; init; } = 20;
        public int StreamSetupSeconds { get; init; } = 10;
        public int CloseSeconds { get; init; } = 8;

        public int MaxSendBytes { get; init; } = 4 * 1024 * 1024;
        public int MaxReceiveBytes { get; init; } = 32 * 1024 * 1024;
    }
}
