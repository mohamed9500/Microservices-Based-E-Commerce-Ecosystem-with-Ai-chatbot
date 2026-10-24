using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nexus.BuildingBlocks.Web;

namespace Nexus.Orders.Clients;

public sealed record CartLine(Guid ProductId, string Sku, string Name, string ImageUrl, decimal UnitPrice, int Quantity);
public sealed record CartSnapshot(IReadOnlyList<CartLine> Items, decimal Subtotal, string Currency);

public interface ICartClient
{
    /// <summary>Reads the caller's cart (prices re-validated server-side by the Cart service) using the caller's bearer token.</summary>
    Task<CartSnapshot> GetCartAsync(string bearerToken, CancellationToken ct);
}

public sealed class CartClient(HttpClient http) : ICartClient
{
    public async Task<CartSnapshot> GetCartAsync(string bearerToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/cart");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var res = await http.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedException();
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<CartSnapshot>(JsonDefaults.Options, ct)
               ?? throw new InvalidOperationException("Cart service returned an empty response.");
    }
}
