namespace LLMSessionGateway.API.Auth.Authentication.Configs
{
    public sealed record JwtValidationConfigs
    {
        public const string SectionName = "Auth:Jwt";

        public string Authority { get; init; } = default!; 
        public string Audience { get; init; } = default!; 

        public int ClockSkewSeconds { get; init; } = 30;
        public bool RequireSub { get; init; } = true;
        public bool RequireHttpsMetadata { get; init; } = true;

        public ClaimNamesConfigs ClaimNames { get; init; } = new();
    }
}
