using System.Net.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Jobs.Core.CustomHealthChecks;

public class VaultHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var httpClient = httpClientFactory.CreateClient();
        //var response = await httpClient.GetAsync("http://127.0.0.1:8200/v1/sys/health", cancellationToken);
        var responseResult = await httpClient.GetFromJsonAsync<VaultHealthCheckResponse>("http://127.0.0.1:8200/v1/sys/health", cancellationToken: cancellationToken);
        //response.IsSuccessStatusCode && 
        if (responseResult is { Initialized: true, Sealed: false })
        {
            return HealthCheckResult.Healthy($"Vault endpoints is healthy.");
        }

        return HealthCheckResult.Unhealthy("Vault endpoint is unhealthy");
    }
}