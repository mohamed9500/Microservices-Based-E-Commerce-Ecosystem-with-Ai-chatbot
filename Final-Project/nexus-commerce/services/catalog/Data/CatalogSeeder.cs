using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Web;
using Nexus.Catalog.Domain;
using Nexus.Contracts;

namespace Nexus.Catalog.Data;

public static class CatalogSeeder
{
    private sealed record SeedFile(List<SeedCategory> Categories, List<SeedProduct> Products);
    private sealed record SeedCategory(int N, string Slug, string Name, string Description);
    private sealed record SeedProduct(int N, string Sku, string Name, string Category, string Brand, decimal Price,
        bool Featured, string Description, Dictionary<string, string> Specs);

    public static async Task SeedAsync(CatalogDb db)
    {
        if (await db.Categories.AnyAsync()) return; // seed once

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("seed-products.json")
            ?? throw new InvalidOperationException("Embedded seed-products.json not found.");
        var seed = await JsonSerializer.DeserializeAsync<SeedFile>(stream, JsonDefaults.Options)
            ?? throw new InvalidOperationException("seed-products.json is empty.");

        var categories = seed.Categories.ToDictionary(c => c.Slug, c => new Category
        {
            Id = SeedIds.Category(c.N), Name = c.Name, Slug = c.Slug, Description = c.Description,
            ImageUrl = $"/images/categories/{c.Slug}.svg", SortOrder = c.N
        });
        db.Categories.AddRange(categories.Values);

        foreach (var p in seed.Products)
        {
            var cat = categories[p.Category];
            var n = p.N.ToString("D2");
            db.Products.Add(new Product
            {
                Id = SeedIds.Product(p.N), Sku = p.Sku, Name = p.Name, Slug = CatalogMapping.Slugify(p.Name),
                Description = p.Description, Price = p.Price, Currency = "USD", CategoryId = cat.Id, Brand = p.Brand,
                ImageUrl = $"/images/products/p{n}.svg",
                GalleryJson = JsonSerializer.Serialize(new[] { $"/images/products/p{n}.svg", $"/images/products/p{n}-b.svg", $"/images/products/p{n}-c.svg" }),
                SpecsJson = JsonSerializer.Serialize(p.Specs), IsActive = true, IsFeatured = p.Featured,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-p.N), UpdatedAtUtc = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }
}
