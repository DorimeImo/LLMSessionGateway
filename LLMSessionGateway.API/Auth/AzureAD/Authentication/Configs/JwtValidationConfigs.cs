using LLMSessionGateway.API.Auth.AzureAD.Authentication.Configs;

namespace LLMSessionGateway.API.Auth.Authentication.AzureAD.Configs
{
    public sealed record JwtValidationConfigs
    {
        public const string SectionName = "Auth:AzureJwt";

        public string Authority { get; init; } = default!; 
        public string Audience { get; init; } = default!; 

        public int ClockSkewSeconds { get; init; } = 30;
        public bool RequireSub { get; init; } = true;
        public bool RequireHttpsMetadata { get; init; } = true;

        public ClaimNames ClaimNames { get; init; } = new();
    }
}
