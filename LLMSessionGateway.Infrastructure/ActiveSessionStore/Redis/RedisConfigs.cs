using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage
{
    public class RedisConfigs
    {
        [Required] public string ConnectionString { get; set; } = default!;
        [Required] public int LockTtlSeconds { get; set; }
        [Required] public int ActiveSessionTtlSeconds { get; set; }
    }
}
