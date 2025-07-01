using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class FileLoggingConfig
    {
        public string BasePath { get; set; } = "Logs";
        public string FileNamePattern { get; set; } = "log-.txt";
        public string RollingInterval { get; set; } = "Day";
    }
}
