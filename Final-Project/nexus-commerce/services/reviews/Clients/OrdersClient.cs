using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nexus.BuildingBlocks.Web;

namespace Nexus.Reviews.Clients;

public interface IOrdersClient
{
    Task<bool> HasPurchasedAsync(string bearerToken, Guid productId, CancellationToken ct);
}

public sealed class OrdersClient(HttpClient http) : IOrdersClient
{
    private sealed record Result(bool Purchased);

    public async Task<bool> HasPurchasedAsync(string bearerToken, Guid productId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"api/orders/has-purchased/{productId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Result>(JsonDefaults.Options, ct))?.Purchased ?? false;
    }
}
