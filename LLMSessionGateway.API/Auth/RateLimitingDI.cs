using LLMSessionGateway.API.Auth.Helpers;
using System.Threading.RateLimiting;

namespace LLMSessionGateway.API.Auth
{
    public static class RateLimitingDI
    {
        public const string PolicyCombined = "chat-combined"; 

        public static IServiceCollection AddGatewayDefenseInDepthRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.AddPolicy<string>(PolicyCombined, httpContext =>
                {
                    var key = SafeKey(httpContext);

                    return RateLimitPartition.Get(key, _ =>
                    {
                        // 1) Single in-flight request per user
                        var conc = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
                        {
                            PermitLimit = 1,
                            QueueLimit = 0,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                        });

                        // 2) Small fixed window per user (DoS guard)
                        var fixedWindow = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });

                        return RateLimiter.CreateChained(conc, fixedWindow);
                    });
                });

                // Global handler to enrich 429s
                options.OnRejected = async (ctx, token) =>
                {
                    ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    ctx.HttpContext.Response.ContentType = "application/json";

                    string? retryAfter = null;
                    if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra))
                    {
                        var seconds = (int)Math.Ceiling(ra.TotalSeconds);
                        retryAfter = $"{seconds}s";
                        ctx.HttpContext.Response.Headers["Retry-After"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    var endpoint = ctx.HttpContext.GetEndpoint()?.DisplayName;
                    var payload = System.Text.Json.JsonSerializer.Serialize(new { error = "RATE_LIMITED", endpoint, retryAfter });
                    await ctx.HttpContext.Response.WriteAsync(payload, token);
                };
            });

            return services;
        }

        private static string SafeKey(HttpContext httpContext)
        {
            try { return SubIssUserPartitionKeyHelper.GetUserIdOrThrow(httpContext.User); }
            catch { return "unauthenticated"; }
        }
    }
}
