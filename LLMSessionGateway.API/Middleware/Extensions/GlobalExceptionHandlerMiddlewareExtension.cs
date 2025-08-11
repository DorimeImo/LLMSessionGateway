using LLMSessionGateway.API.Middleware;

public static class GlobalExceptionHandlerMiddlewareExtension
{
    public static IApplicationBuilder UseGlobalExceptionHandlerMiddlewareExtension(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}