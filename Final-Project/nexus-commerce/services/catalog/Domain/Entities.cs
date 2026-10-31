namespace Nexus.Catalog.Domain;

public sealed class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public int SortOrder { get; set; }
    public List<Product> Products { get; set; } = [];
}

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Brand { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    /// <summary>JSON array of image URLs (kept denormalised: always read together with the product).</summary>
    public string GalleryJson { get; set; } = "[]";
    /// <summary>JSON object of specification name -> value.</summary>
    public string SpecsJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
