using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.ActiveSessionStore.AzureBlobStorage
{
    public class RedisConfigs
    {
        public int LockTtlSeconds { get; set; }
        public int ActiveSessionTtlSeconds { get; set; }
    }
}
