using LLMSessionGateway.API;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Tests.IntegrationTests.Helpers
{
    public class ApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.UseContentRoot(GetContentRootPath());
            return base.CreateHost(builder);
        }

        private static string GetContentRootPath()
        {
            // Adjust if your folder structure is different
            var relativePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                @"..\..\..\..\LLMSessionGateway.API");

            return Path.GetFullPath(relativePath);
        }
    }
}
