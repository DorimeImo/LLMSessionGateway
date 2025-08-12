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
        [Required] public string Host { get; set; } = default!;
        [Required] public int Port { get; set; }
    }
    public sealed class GrpcTimeoutsOptions
    {
        [Required] public TimeSpan OpenSession { get; set; } = TimeSpan.FromSeconds(5);
        [Required] public TimeSpan SendMessage { get; set; } = TimeSpan.FromSeconds(10);
        [Required] public TimeSpan StreamReplySetup { get; set; } = TimeSpan.FromSeconds(10);
        [Required] public TimeSpan CloseSession { get; set; } = TimeSpan.FromSeconds(5);
    }
}
