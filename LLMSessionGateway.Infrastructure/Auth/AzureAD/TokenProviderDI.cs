using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Auth.AzureAD
{
    public static class TokenProviderDI
    {
        public static IServiceCollection AddAzureTokenProvider(this IServiceCollection services)
        {
            services.AddSingleton<TokenCredential>(_ =>
            {
                var opts = new DefaultAzureCredentialOptions { ExcludeInteractiveBrowserCredential = true };

                var uamiResId = Environment.GetEnvironmentVariable("AZURE_MANAGED_IDENTITY_RESOURCE_ID");
                if (!string.IsNullOrWhiteSpace(uamiResId))
                {
                    try { opts.ManagedIdentityResourceId = new ResourceIdentifier(uamiResId); }
                    catch (FormatException ex)
                    {
                        throw new InvalidOperationException("AZURE_MANAGED_IDENTITY_RESOURCE_ID is not a valid ARM resource ID.", ex);
                    }
                }

                var uamiClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
                if (!string.IsNullOrWhiteSpace(uamiClientId)) 
                    opts.ManagedIdentityClientId = uamiClientId;

                return new DefaultAzureCredential(opts);
            });

            services.AddSingleton<ITokenProvider, TokenProvider>();
            return services;
        }
    }
}
