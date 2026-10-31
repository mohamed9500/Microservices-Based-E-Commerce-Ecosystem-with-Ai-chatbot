using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Nexus.BuildingBlocks.Web;

namespace Nexus.Catalog.Domain;

public sealed record CategoryDto(Guid Id, string Name, string Slug, string? Description, string? ImageUrl, int SortOrder);

public sealed record ProductDto(Guid Id, string Sku, string Name, string Slug, string Description, decimal Price,
    string Currency, Guid CategoryId, string CategoryName, string CategorySlug, string Brand, string ImageUrl,
    IReadOnlyList<string> Gallery, IReadOnlyDictionary<string, string> Specs, bool IsActive, bool IsFeatured,
    DateTime CreatedAtUtc);

public sealed record ProductQuery(string? Q, string? Category, Guid? CategoryId, decimal? MinPrice, decimal? MaxPrice,
    string? Brand, bool? Featured, bool? IncludeInactive, string? Sort, int? Page, int? PageSize);

public sealed record ProductUpsertRequest
{
    [Required, StringLength(64, MinimumLength = 2)] public string Sku { get; init; } = "";
    [Required, StringLength(200, MinimumLength = 2)] public string Name { get; init; } = "";
    [Required, StringLength(4000, MinimumLength = 10)] public string Description { get; init; } = "";
    [Range(0.01, 1_000_000)] public decimal Price { get; init; }
    public Guid CategoryId { get; init; }
    [Required, StringLength(100)] public string Brand { get; init; } = "";
    [Required, StringLength(500)] public string ImageUrl { get; init; } = "";
    public List<string>? Gallery { get; init; }
    public Dictionary<string, string>? Specs { get; init; }
    public bool IsActive { get; init; } = true;
    public bool IsFeatured { get; init; }
}

public sealed record CategoryUpsertRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; init; } = "";
    [StringLength(500)] public string? Description { get; init; }
    [StringLength(500)] public string? ImageUrl { get; init; }
    [Range(0, 1000)] public int SortOrder { get; init; }
}

public static class CatalogMapping
{
    public static CategoryDto ToDto(this Category c) => new(c.Id, c.Name, c.Slug, c.Description, c.ImageUrl, c.SortOrder);

    public static ProductDto ToDto(this Product p) => new(p.Id, p.Sku, p.Name, p.Slug, p.Description, p.Price, p.Currency,
        p.CategoryId, p.Category?.Name ?? "", p.Category?.Slug ?? "", p.Brand, p.ImageUrl,
        Parse<List<string>>(p.GalleryJson) ?? [],
        Parse<Dictionary<string, string>>(p.SpecsJson) ?? new Dictionary<string, string>(),
        p.IsActive, p.IsFeatured, p.CreatedAtUtc);

    private static T? Parse<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options); }
        catch (JsonException) { return default; }
    }

    public static string Slugify(string value)
    {
        var chars = value.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '-').ToArray();
        var slug = string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return slug.Length == 0 ? "item" : (slug.Length > 100 ? slug[..100] : slug);
    }
}
