using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.HealthChecks
{
    public class GrpcEndpointHealthCheck : IHealthCheck
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _timeoutMs;

        public GrpcEndpointHealthCheck(string host, int port, int timeoutMs = 500)
        {
            _host = host;
            _port = port;
            _timeoutMs = timeoutMs;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken ct = default)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(_host, _port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(_timeoutMs, ct));
                if (completed == connectTask && client.Connected)
                    return HealthCheckResult.Healthy("gRPC endpoint reachable");

                return HealthCheckResult.Unhealthy("gRPC endpoint not reachable (timeout)");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("gRPC endpoint not reachable", ex);
            }
        }
    }
}
