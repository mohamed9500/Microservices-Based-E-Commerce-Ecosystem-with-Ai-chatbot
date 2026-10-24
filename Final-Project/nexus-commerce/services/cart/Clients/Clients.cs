using System.Net.Http.Json;
using Nexus.BuildingBlocks.Web;

namespace Nexus.Cart.Clients;

public sealed record CatalogProduct(Guid Id, string Sku, string Name, decimal Price, string Currency, string ImageUrl, bool IsActive);
public sealed record StockInfo(Guid ProductId, int Available, string Status);

public interface ICatalogClient
{
    Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(IEnumerable<Guid> ids, CancellationToken ct);
}

public interface IInventoryClient
{
    Task<IReadOnlyList<StockInfo>> GetStockAsync(IEnumerable<Guid> ids, CancellationToken ct);
}

public sealed class CatalogClient(HttpClient http) : ICatalogClient
{
    public async Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return [];
        var result = await http.GetFromJsonAsync<List<CatalogProduct>>($"api/products/by-ids?ids={string.Join(',', list)}", JsonDefaults.Options, ct);
        return result ?? [];
    }
}

public sealed class InventoryClient(HttpClient http) : IInventoryClient
{
    public async Task<IReadOnlyList<StockInfo>> GetStockAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return [];
        return await http.GetFromJsonAsync<List<StockInfo>>($"api/inventory/stock?ids={string.Join(',', list)}", JsonDefaults.Options, ct) ?? [];
    }
}
