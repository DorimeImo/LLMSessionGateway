using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Grpc
{
    public sealed class GrpcConfigs
    {
        public string Host { get; set; } = default!;
        public int Port { get; set; }
    }
    public sealed class GrpcTimeoutsOptions
    {
        public TimeSpan OpenSession { get; set; } = TimeSpan.FromSeconds(5);
        public TimeSpan SendMessage { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan StreamReplySetup { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan CloseSession { get; set; } = TimeSpan.FromSeconds(5);
    }
}
