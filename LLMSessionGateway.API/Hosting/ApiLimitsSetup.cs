using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;

namespace LLMSessionGateway.API.Hosting
{
    public static class ApiLimitsSetup
    {
        public static IServiceCollection AddApiLimits(this IServiceCollection services, IConfiguration config)
        {
            services.Configure<ApiLimitsOptions>(config.GetSection("ApiLimits"));

            services.AddOptions<KestrelServerOptions>()
                .Configure<IOptions<ApiLimitsOptions>>((kestrel, limits) =>
                {
                    kestrel.Limits.MaxRequestBodySize = limits.Value.MaxRequestBodySizeBytes;
                    kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
                });

            services.AddOptions<JsonOptions>()
                .Configure<IOptions<ApiLimitsOptions>>((json, limits) =>
                    json.JsonSerializerOptions.MaxDepth = limits.Value.JsonMaxDepth);

            return services;
        }
    }
}


