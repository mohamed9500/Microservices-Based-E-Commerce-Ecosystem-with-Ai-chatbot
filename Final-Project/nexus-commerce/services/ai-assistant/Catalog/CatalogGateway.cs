using System.Globalization;
using System.Net.Http.Json;
using Nexus.BuildingBlocks.Web;

namespace Nexus.Assistant.Catalog;

/// <summary>A catalog product as retrieved from the real services. All text is sanitised (untrusted input).</summary>
public sealed record ProductFact(Guid Id, string Sku, string Name, string Brand, string Category, string CategorySlug,
    decimal Price, string Currency, string Description, IReadOnlyDictionary<string, string> Specs, string ImageUrl,
    string Availability, double? Rating, int RatingCount);

public sealed record CategoryFact(string Slug, string Name);
public sealed record ProductSearch(string? Query, string? CategorySlug, decimal? MinPrice, decimal? MaxPrice, string? Sort, int Limit = 5);

/// <summary>Read-only access to catalog facts. The assistant has no write path to any service.</summary>
public interface ICatalogGateway
{
    Task<IReadOnlyList<ProductFact>> SearchAsync(ProductSearch search, CancellationToken ct);
    Task<IReadOnlyList<ProductFact>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<CategoryFact>> GetCategoriesAsync(CancellationToken ct);
}

public static class TextSanitizer
{
    /// <summary>Strips control characters, collapses whitespace and truncates. Defence in depth for prompt-injection payloads in data.</summary>
    public static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var chars = value.Select(c => char.IsControl(c) ? ' ' : c).ToArray();
        var s = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return s.Length <= maxLength ? s : s[..maxLength] + "...";
    }
}

public sealed class HttpCatalogGateway(IHttpClientFactory factory, ILogger<HttpCatalogGateway> logger) : ICatalogGateway
{
    public const string CatalogClient = "catalog", InventoryClient = "inventory", ReviewsClient = "reviews";

    private sealed record Paged(List<Item> Items);
    private sealed record Item(Guid Id, string Sku, string Name, string Description, decimal Price, string Currency,
        string CategoryName, string CategorySlug, string Brand, string ImageUrl, Dictionary<string, string>? Specs);
    private sealed record Stock(Guid ProductId, int Available, string Status);
    private sealed record Rating(Guid ProductId, double Average, int Count);

    public async Task<IReadOnlyList<ProductFact>> SearchAsync(ProductSearch s, CancellationToken ct)
    {
        var inv = CultureInfo.InvariantCulture;
        var q = new List<string> { $"pageSize={Math.Clamp(s.Limit, 1, 20)}" };
        if (!string.IsNullOrWhiteSpace(s.Query)) q.Add($"q={Uri.EscapeDataString(s.Query!)}");
        if (!string.IsNullOrWhiteSpace(s.CategorySlug)) q.Add($"category={Uri.EscapeDataString(s.CategorySlug!)}");
        if (s.MinPrice.HasValue) q.Add($"minPrice={s.MinPrice.Value.ToString(inv)}");
        if (s.MaxPrice.HasValue) q.Add($"maxPrice={s.MaxPrice.Value.ToString(inv)}");
        if (!string.IsNullOrWhiteSpace(s.Sort)) q.Add($"sort={Uri.EscapeDataString(s.Sort!)}");
        var page = await factory.CreateClient(CatalogClient).GetFromJsonAsync<Paged>($"api/products?{string.Join('&', q)}", JsonDefaults.Options, ct);
        return await EnrichAsync(page?.Items ?? [], ct);
    }

    public async Task<IReadOnlyList<ProductFact>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().Take(10).ToList();
        if (list.Count == 0) return [];
        var items = await factory.CreateClient(CatalogClient)
            .GetFromJsonAsync<List<Item>>($"api/products/by-ids?ids={string.Join(',', list)}", JsonDefaults.Options, ct);
        return await EnrichAsync(items ?? [], ct);
    }

    public async Task<IReadOnlyList<CategoryFact>> GetCategoriesAsync(CancellationToken ct)
    {
        var cats = await factory.CreateClient(CatalogClient).GetFromJsonAsync<List<CategoryFact>>("api/categories", JsonDefaults.Options, ct);
        return cats?.Select(c => new CategoryFact(c.Slug, TextSanitizer.Clean(c.Name, 80))).ToList() ?? [];
    }

    private async Task<IReadOnlyList<ProductFact>> EnrichAsync(List<Item> items, CancellationToken ct)
    {
        if (items.Count == 0) return [];
        var ids = string.Join(',', items.Select(i => i.Id));
        var stock = new Dictionary<Guid, string>();
        var ratings = new Dictionary<Guid, Rating>();
        try
        {
            var s = await factory.CreateClient(InventoryClient).GetFromJsonAsync<List<Stock>>($"api/inventory/stock?ids={ids}", JsonDefaults.Options, ct);
            foreach (var x in s ?? []) stock[x.ProductId] = x.Status;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Inventory lookup failed; availability reported as unknown");
        }
        try
        {
            var r = await factory.CreateClient(ReviewsClient).GetFromJsonAsync<List<Rating>>($"api/reviews/summary?ids={ids}", JsonDefaults.Options, ct);
            foreach (var x in r ?? []) ratings[x.ProductId] = x;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Review summary lookup failed; ratings omitted");
        }

        return items.Select(i => new ProductFact(i.Id, TextSanitizer.Clean(i.Sku, 64), TextSanitizer.Clean(i.Name, 120),
            TextSanitizer.Clean(i.Brand, 60), TextSanitizer.Clean(i.CategoryName, 80), i.CategorySlug, i.Price, i.Currency,
            TextSanitizer.Clean(i.Description, 300),
            (i.Specs ?? new()).Take(8).ToDictionary(k => TextSanitizer.Clean(k.Key, 40), k => TextSanitizer.Clean(k.Value, 80)),
            i.ImageUrl, stock.GetValueOrDefault(i.Id, "unknown"),
            ratings.TryGetValue(i.Id, out var rt) && rt.Count > 0 ? rt.Average : null,
            ratings.TryGetValue(i.Id, out var rc) ? rc.Count : 0)).ToList();
    }
}
