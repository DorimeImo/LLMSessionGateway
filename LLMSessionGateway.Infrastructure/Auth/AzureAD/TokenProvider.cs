using Azure.Core;
using Azure.Identity;
using LLMSessionGateway.Core.Utilities.Functional;
using Observability.Shared.Contracts;
using Observability.Shared.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LLMSessionGateway.Infrastructure.Auth.AzureAD
{
    public sealed class TokenProvider : ITokenProvider
    {
        private readonly TokenCredential _credential;
        private readonly Serilog.ILogger _logger;
        private readonly ConcurrentDictionary<string, AccessToken> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

        // Refresh a bit before expiry to avoid edge races during calls
        private static readonly TimeSpan EarlyRefresh = TimeSpan.FromMinutes(5);

        public TokenProvider(
            Serilog.ILogger logger,
            TokenCredential? credential = null)
        {
            _logger = logger;
            _credential = credential ?? new DefaultAzureCredential();
        }

        public async Task<Result<string>> GetTokenAsync(string audienceAndScope, CancellationToken ct = default)
        {
            var (source, operation) = CallerInfo.GetCallerClassAndMethod();
            var tracingOperationName = TracingOperationNameBuilder.TracingOperationNameBuild((source, operation));

            try
            {
                ct.ThrowIfCancellationRequested();

                var scope = audienceAndScope;

                if (_cache.TryGetValue(scope, out var cached) &&
                    cached.ExpiresOn > DateTimeOffset.UtcNow + EarlyRefresh)
                {
                    return Result<string>.Success(cached.Token);
                }

                var gate = _locks.GetOrAdd(scope, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(ct).ConfigureAwait(false);

                try
                {
                    if (_cache.TryGetValue(scope, out cached) &&
                        cached.ExpiresOn > DateTimeOffset.UtcNow + EarlyRefresh)
                    {
                        return Result<string>.Success(cached.Token);
                    }

                    var request = new TokenRequestContext(new[] { scope });
                    var token = await _credential.GetTokenAsync(request, ct).ConfigureAwait(false);

                    _cache[scope] = token;

                    return Result<string>.Success(token.Token);
                }
                finally
                {
                    gate.Release();
                }
            }
            catch (Exception ex)
            {
                return TokenErrorHandler.Handle<string>(ex, _logger);
            }
        }
    }
}
