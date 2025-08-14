namespace LLMSessionGateway.API.Auth.Authentication.Configs
{
    public sealed record ClaimNamesConfigs
    {
        public string Scope { get; init; } = "scope";
        public string Sub { get; init; } = "sub";
    }
}
