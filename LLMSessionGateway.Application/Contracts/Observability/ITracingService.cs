namespace LLMSessionGateway.Application.Contracts.Observability
{
    public interface ITracingService
    {
        void ExtractTraceIdToLogContext(string operationName);
        IDisposable? StartActivity(string operationName);
        void InjectTraceContextIntoHttpResponse();
    }
}
