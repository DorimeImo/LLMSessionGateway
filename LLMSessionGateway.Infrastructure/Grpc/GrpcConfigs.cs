using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Grpc
{
    public class GrpcConfigs
    {
        public string Host { get; set; } = default!;
        public int Port { get; set; }
    }
}
