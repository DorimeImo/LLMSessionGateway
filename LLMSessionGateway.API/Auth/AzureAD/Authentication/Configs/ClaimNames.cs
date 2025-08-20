

namespace LLMSessionGateway.API.Auth.AzureAD.Authentication.Configs
{
    public sealed record ClaimNames 
    {
        public string Scope { get; init; } = "scp";
        public string Sub { get; init; } = "sub";
    }
}
