using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class LogToAppInsightsConfig
    {
        [Required] public string AppInsightsConnectionString { get; set; } = "Logs";
        [Required] public string FileNamePattern { get; set; } = "log-.txt";
        [Required] public string RollingInterval { get; set; } = "Day";
    }
}
