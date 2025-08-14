using LLMSessionGateway.API.Middleware;

public static class GlobalExceptionHandlerMiddlewareExtension
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}