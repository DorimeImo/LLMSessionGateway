using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class JaegerConfigs
    {
        [Required] public string AgentHost { get; set; } = default!;
        [Required] public int AgentPort { get; set; }
    }
}
