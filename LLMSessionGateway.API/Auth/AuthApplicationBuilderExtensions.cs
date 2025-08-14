namespace LLMSessionGateway.API.Auth
{
    public static class AuthApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseApiAuth(this IApplicationBuilder app)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            return app;
        }
    }
}
