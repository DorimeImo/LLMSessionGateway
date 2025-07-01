using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class JaegerConfigs
    {
        public string AgentHost { get; set; } = default!;
        public int AgentPort { get; set; }
    }
}
