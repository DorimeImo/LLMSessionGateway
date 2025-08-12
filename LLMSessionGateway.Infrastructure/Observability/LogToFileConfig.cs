using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class FileLoggingConfig
    {
        [Required] public string BasePath { get; set; } = "Logs";
        [Required] public string FileNamePattern { get; set; } = "log-.txt";
        [Required] public string RollingInterval { get; set; } = "Day";
    }
}
