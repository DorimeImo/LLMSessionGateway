using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Observability
{
    public class AzureAppInsightsConfigs
    {
        [Required] public string ConnectionString { get; set; } = default!;
    }
}
