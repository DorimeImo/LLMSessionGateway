namespace LLMSessionGateway.API.Auth.Authentication.Configs
{
    public sealed record ClaimNamesConfigs
    {
        public string Scope { get; init; } = "scp";
        public string Sub { get; init; } = "sub";
    }
}
