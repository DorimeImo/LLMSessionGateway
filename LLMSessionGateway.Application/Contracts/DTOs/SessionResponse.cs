namespace LLMSessionGateway.Application.Contracts.DTOs
{
    public class SessionResponse
    {
        public string SessionId { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }
}
