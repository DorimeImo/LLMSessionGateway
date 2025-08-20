using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class LogConfigs
    {
        [Required] public string FileNamePattern { get; set; } = "logs/log-.txt";
        [Required] public string RollingInterval { get; set; } = "Day";
    }
}
