using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.SliceIntegrationTests
{
    public static class InMemoryConfigurations
    {
        public static Dictionary<string, string?> CreateInMemoryConfigurations()
        {
            return new Dictionary<string, string?>
            {
                // General
                ["Redis:LockTtlSeconds"] = "15",
                ["Redis:ActiveSessionTtlSeconds"] = "1800",

                ["Grpc:ChatService:Host"] = "localhost",
                ["Grpc:ChatService:Port"] = "50051",
                ["Grpc:ChatService:UseTls"] = "true",
                ["Grpc:ChatService:EnableMtls"] = "false",
                ["Grpc:ChatService:ClientCertificateBase64Env"] = "PFX_B64",
                ["Grpc:ChatService:ClientCertificatePasswordEnv"] = "PFX_PWD",
                ["Grpc:ChatService:Scope"] = "api://chat-backend/.default",

                ["Grpc:Timeouts:OpenSeconds"] = "10",
                ["Grpc:Timeouts:SendSeconds"] = "20",
                ["Grpc:Timeouts:StreamSetupSeconds"] = "10",
                ["Grpc:Timeouts:CloseSeconds"] = "8",
                ["Grpc:Timeouts:MaxSendBytes"] = "4194304",
                ["Grpc:Timeouts:MaxReceiveBytes"] = "33554432",

                ["ApiLimits:MaxRequestBodySizeBytes"] = "1048576",
                ["ApiLimits:JsonMaxDepth"] = "32",

                ["Logging:LogConfigs:FileNamePattern"] = "log-.txt",                  // filename-only works well with Serilog rolling
                ["Logging:LogConfigs:RollingInterval"] = "Day",

                // Azure
                ["AzureBlob:ContainerName"] = "archive-sessions",
                ["AzureBlob:BlobAccountUrl"] = "https://unit-tests.blob.core.windows.net",

                ["Auth:AzureJwt:Authority"] = "https://login.microsoftonline.com/common/v2.0",
                ["Auth:AzureJwt:Audience"] = "api://llmsessiongateway-api",
                ["Auth:AzureJwt:ClockSkewSeconds"] = "30",
                ["Auth:AzureJwt:RequireSub"] = "true",
                ["Auth:AzureJwt:RequireHttpsMetadata"] = "true",
                ["Auth:AzureJwt:ClaimNames:Scope"] = "scp",
                ["Auth:AzureJwt:ClaimNames:Sub"] = "sub"
            };
        }
    }
}
