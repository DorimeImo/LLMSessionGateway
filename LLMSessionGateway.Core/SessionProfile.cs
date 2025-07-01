using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Core
{
    public class SessionProfile
    {
        public required string UserId { get; init; }
        public string? PreferredModel { get; init; }
        public float Temperature { get; init; }
        public float TopP { get; init; }
        public string? Personality { get; init; }
        public Dictionary<string, string> CustomMetadata { get; init; } = new();
    }
}
