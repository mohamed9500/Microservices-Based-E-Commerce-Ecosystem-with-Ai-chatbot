using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Web;
using Nexus.Catalog.Data;
using Nexus.Catalog.Domain;

namespace Nexus.Catalog.Endpoints;

public static class CatalogEndpoints
{
    private const string CategoriesCacheKey = "catalog:categories:v1";

    public static void MapCatalogEndpoints(this WebApplication app)
    {
        MapCategories(app);
        MapProducts(app);
    }

    private static void MapCategories(WebApplication app)
    {
        var g = app.MapGroup("/api/categories").WithTags("Categories");

        g.MapGet("/", async (CatalogDb db, IDistributedCache cache, ILogger<CatalogDb> log, CancellationToken ct) =>
        {
            var list = await cache.GetOrCreateAsync<List<CategoryDto>>(CategoriesCacheKey, TimeSpan.FromMinutes(5),
                async c => (await db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(c))
                    .Select(x => x.ToDto()).ToList(), log, ct);
            return Results.Ok(list);
        });

        g.MapPost("/", async (CategoryUpsertRequest? req, CatalogDb db, IDistributedCache cache, ILogger<CatalogDb> log, CancellationToken ct) =>
        {
            req.Validated();
            if (req!.ImageUrl != null && !Validation.IsSafeImageUrl(req.ImageUrl))
                throw new ValidationFailedException("imageUrl", "Image URL must be a relative path or an https URL.");
            var slug = await UniqueSlugAsync(db.Categories.Select(c => c.Slug), CatalogMapping.Slugify(req.Name), ct);
            var cat = new Category { Name = req.Name.Trim(), Slug = slug, Description = req.Description, ImageUrl = req.ImageUrl, SortOrder = req.SortOrder };
            db.Categories.Add(cat);
            await db.SaveChangesAsync(ct);
            await cache.InvalidateAsync(log, CategoriesCacheKey);
            return Results.Created($"/api/categories/{cat.Id}", cat.ToDto());
        }).RequireAuthorization("admin");

        g.MapPut("/{id:guid}", async (Guid id, CategoryUpsertRequest? req, CatalogDb db, IDistributedCache cache, ILogger<CatalogDb> log, CancellationToken ct) =>
        {
            req.Validated();
            if (req!.ImageUrl != null && !Validation.IsSafeImageUrl(req.ImageUrl))
                throw new ValidationFailedException("imageUrl", "Image URL must be a relative path or an https URL.");
            var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Category not found.");
            cat.Name = req.Name.Trim(); cat.Description = req.Description; cat.ImageUrl = req.ImageUrl; cat.SortOrder = req.SortOrder;
            await db.SaveChangesAsync(ct);
            await cache.InvalidateAsync(log, CategoriesCacheKey);
            return Results.Ok(cat.ToDto());
        }).RequireAuthorization("admin");

        g.MapDelete("/{id:guid}", async (Guid id, CatalogDb db, IDistributedCache cache, ILogger<CatalogDb> log, CancellationToken ct) =>
        {
            var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Category not found.");
            if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
                throw new ConflictException("This category still contains products. Move or remove them first.");
            db.Categories.Remove(cat);
            await db.SaveChangesAsync(ct);
            await cache.InvalidateAsync(log, CategoriesCacheKey);
            return Results.NoContent();
        }).RequireAuthorization("admin");
    }

    private static void MapProducts(WebApplication app)
    {
        var g = app.MapGroup("/api/products").WithTags("Products");

        g.MapGet("/", async (HttpContext http, CatalogDb db, [AsParameters] ProductQuery q, CancellationToken ct) =>
        {
            var query = db.Products.AsNoTracking().Include(p => p.Category).AsQueryable();
            var admin = http.User.Identity?.IsAuthenticated == true && http.User.IsAdmin();
            if (!(q.IncludeInactive == true && admin)) query = query.Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(q.Q))
            {
                foreach (var raw in q.Q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
                {
                    var t = raw.Length > 50 ? raw[..50] : raw;
                    query = query.Where(p => p.Name.Contains(t) || p.Description.Contains(t) || p.Brand.Contains(t) ||
                                             p.Sku.Contains(t) || p.Category.Name.Contains(t));
                }
            }
            if (!string.IsNullOrWhiteSpace(q.Category)) query = query.Where(p => p.Category.Slug == q.Category);
            if (q.CategoryId.HasValue) query = query.Where(p => p.CategoryId == q.CategoryId.Value);
            if (q.MinPrice.HasValue) query = query.Where(p => p.Price >= q.MinPrice.Value);
            if (q.MaxPrice.HasValue) query = query.Where(p => p.Price <= q.MaxPrice.Value);
            if (!string.IsNullOrWhiteSpace(q.Brand)) query = query.Where(p => p.Brand == q.Brand);
            if (q.Featured == true) query = query.Where(p => p.IsFeatured);

            query = q.Sort?.ToLowerInvariant() switch
            {
                "price_asc" => query.OrderBy(p => p.Price).ThenBy(p => p.Name),
                "price_desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.Name),
                "name" => query.OrderBy(p => p.Name),
                "newest" => query.OrderByDescending(p => p.CreatedAtUtc),
                _ => query.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.Name)
            };

            var page = await query.ToPagedAsync(q.Page, q.PageSize, ct, 12, 50);
            return Results.Ok(page.Map(p => p.ToDto()));
        });

        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, CatalogDb db, CancellationToken ct) =>
        {
            var p = await db.Products.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p == null || (!p.IsActive && !(http.User.Identity?.IsAuthenticated == true && http.User.IsAdmin())))
                throw new NotFoundException("Product not found.");
            return Results.Ok(p.ToDto());
        });

        g.MapGet("/by-ids", async (string ids, CatalogDb db, CancellationToken ct) =>
        {
            var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
                .Where(x => x.HasValue).Select(x => x!.Value).Distinct().Take(50).ToList();
            if (parsed.Count == 0) return Results.Ok(Array.Empty<ProductDto>());
            var items = await db.Products.AsNoTracking().Include(p => p.Category)
                .Where(p => parsed.Contains(p.Id)).ToListAsync(ct);
            return Results.Ok(items.Select(p => p.ToDto()).ToList());
        });

        g.MapGet("/brands", async (CatalogDb db, CancellationToken ct) =>
            Results.Ok(await db.Products.AsNoTracking().Where(p => p.IsActive).Select(p => p.Brand).Distinct().OrderBy(b => b).ToListAsync(ct)));

        g.MapPost("/", async (ProductUpsertRequest? req, CatalogDb db, CancellationToken ct) =>
        {
            await ValidateProductAsync(req, db, null, ct);
            var slug = await UniqueSlugAsync(db.Products.Select(p => p.Slug), CatalogMapping.Slugify(req!.Name), ct);
            var p = new Product { Slug = slug };
            Apply(p, req);
            db.Products.Add(p);
            await db.SaveChangesAsync(ct);
            await db.Entry(p).Reference(x => x.Category).LoadAsync(ct);
            return Results.Created($"/api/products/{p.Id}", p.ToDto());
        }).RequireAuthorization("admin");

        g.MapPut("/{id:guid}", async (Guid id, ProductUpsertRequest? req, CatalogDb db, CancellationToken ct) =>
        {
            await ValidateProductAsync(req, db, id, ct);
            var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Product not found.");
            Apply(p, req!);
            await db.SaveChangesAsync(ct);
            await db.Entry(p).Reference(x => x.Category).LoadAsync(ct);
            return Results.Ok(p.ToDto());
        }).RequireAuthorization("admin");

        // Soft delete: order history keeps referencing the product id.
        g.MapDelete("/{id:guid}", async (Guid id, CatalogDb db, CancellationToken ct) =>
        {
            var p = await db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Product not found.");
            p.IsActive = false;
            p.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization("admin");
    }

    private static async Task ValidateProductAsync(ProductUpsertRequest? req, CatalogDb db, Guid? existingId, CancellationToken ct)
    {
        req.Validated();
        var errors = new Dictionary<string, string[]>();
        if (!Validation.IsSafeImageUrl(req!.ImageUrl)) errors["imageUrl"] = ["Image URL must be a relative path or an https URL."];
        if (req.Gallery != null && (req.Gallery.Count > 10 || req.Gallery.Any(u => !Validation.IsSafeImageUrl(u))))
            errors["gallery"] = ["Gallery may contain at most 10 relative or https image URLs."];
        if (req.Specs != null && req.Specs.Count > 30) errors["specs"] = ["At most 30 specifications are allowed."];
        if (!await db.Categories.AnyAsync(c => c.Id == req.CategoryId, ct)) errors["categoryId"] = ["Category does not exist."];
        var sku = req.Sku.Trim();
        if (await db.Products.AnyAsync(p => p.Sku == sku && p.Id != existingId, ct)) errors["sku"] = ["SKU is already in use."];
        if (errors.Count > 0) throw new ValidationFailedException(errors);
    }

    private static void Apply(Product p, ProductUpsertRequest req)
    {
        p.Sku = req.Sku.Trim(); p.Name = req.Name.Trim(); p.Description = req.Description.Trim();
        p.Price = decimal.Round(req.Price, 2); p.CategoryId = req.CategoryId; p.Brand = req.Brand.Trim();
        p.ImageUrl = req.ImageUrl; p.IsActive = req.IsActive; p.IsFeatured = req.IsFeatured;
        p.GalleryJson = JsonSerializer.Serialize(req.Gallery is { Count: > 0 } ? req.Gallery : new List<string> { req.ImageUrl });
        p.SpecsJson = JsonSerializer.Serialize(req.Specs ?? new Dictionary<string, string>());
        p.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static async Task<string> UniqueSlugAsync(IQueryable<string> existing, string baseSlug, CancellationToken ct)
    {
        var slug = baseSlug;
        for (var i = 2; await existing.AnyAsync(s => s == slug, ct); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }
}
