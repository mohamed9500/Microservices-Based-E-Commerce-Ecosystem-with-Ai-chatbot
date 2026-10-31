
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nexus.BuildingBlocks.Data;

public static class HttpClientExtensions
{
    /// <summary>
    /// Typed client for a sibling service:
    /// base address from Services:{name},
    /// correlation forwarding, and standard resilience.
    /// </summary>
    public static IHttpClientBuilder AddServiceClient<TClient, TImpl>(
        this IServiceCollection services,
        IConfiguration config,
        string serviceName)
        where TClient : class
        where TImpl : class, TClient
    {
        var url = config[$"Services:{serviceName}"]
            ?? throw new InvalidOperationException(
                $"Services:{serviceName} is not configured.");

        // Create the typed HTTP client.
        var clientBuilder = services.AddHttpClient<TClient, TImpl>(client =>
        {
            client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Forward the correlation information.
        clientBuilder.AddHttpMessageHandler<Web.CorrelationHandler>();

        // Configure standard resilience without returning its builder.
        clientBuilder.AddStandardResilienceHandler();

        // Return the expected IHttpClientBuilder.
        return clientBuilder;
    }
}
