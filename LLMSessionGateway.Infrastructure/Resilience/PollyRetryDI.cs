using LLMSessionGateway.Application.Contracts.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Resilience
{
    public static class PollyRetryDI
    {
        public static IServiceCollection AddPollyRetryPolicy(this IServiceCollection services, IConfiguration config)
        {
            services.AddSingleton<IRetryPolicyRunner, RetryPolicies>();
            return services;
        }
    }
}
