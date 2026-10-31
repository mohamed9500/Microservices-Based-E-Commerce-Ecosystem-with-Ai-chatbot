using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Cart.Clients;
using Nexus.Cart.Data;
using Nexus.Cart.Domain;

namespace Nexus.Cart.Endpoints;

public static class CartEndpoints
{
    private const int MaxDistinctItems = 50;

    public static void MapCartEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/cart").WithTags("Cart").RequireAuthorization("authenticated");

        g.MapGet("/", async (HttpContext http, CartDb db, ICatalogClient catalog, CancellationToken ct) =>
            Results.Ok(await LoadAndRefreshAsync(http.User.GetUserId(), db, catalog, ct)));

        g.MapPost("/items", async (AddItemRequest? req, HttpContext http, CartDb db, ICatalogClient catalog, IInventoryClient inventory, CancellationToken ct) =>
        {
            req.Validated();
            var userId = http.User.GetUserId();
            var product = (await catalog.GetProductsAsync([req!.ProductId], ct)).FirstOrDefault();
            if (product is not { IsActive: true }) throw new NotFoundException("Product not found or no longer available.");

            var item = await db.Items.FirstOrDefaultAsync(i => i.UserId == userId && i.ProductId == req.ProductId, ct);
            var newQty = (item?.Quantity ?? 0) + req.Quantity;
            if (newQty > 20) throw new ValidationFailedException("quantity", "You can order at most 20 units of one product.");
            await EnsureStockAsync(inventory, product, newQty, ct);

            if (item == null)
            {
                if (await db.Items.CountAsync(i => i.UserId == userId, ct) >= MaxDistinctItems)
                    throw new ValidationFailedException("productId", $"A cart can hold at most {MaxDistinctItems} different products.");
                item = new CartItem { UserId = userId, ProductId = product.Id };
                db.Items.Add(item);
            }
            Apply(item, product, newQty);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAndRefreshAsync(userId, db, catalog, ct));
        });

        g.MapPut("/items/{productId:guid}", async (Guid productId, UpdateItemRequest? req, HttpContext http, CartDb db, ICatalogClient catalog, IInventoryClient inventory, CancellationToken ct) =>
        {
            req.Validated();
            var userId = http.User.GetUserId();
            var item = await db.Items.FirstOrDefaultAsync(i => i.UserId == userId && i.ProductId == productId, ct)
                       ?? throw new NotFoundException("Item is not in your cart.");
            var product = (await catalog.GetProductsAsync([productId], ct)).FirstOrDefault();
            if (product is not { IsActive: true }) throw new NotFoundException("Product is no longer available.");
            await EnsureStockAsync(inventory, product, req!.Quantity, ct);
            Apply(item, product, req.Quantity);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAndRefreshAsync(userId, db, catalog, ct));
        });

        g.MapDelete("/items/{productId:guid}", async (Guid productId, HttpContext http, CartDb db, ICatalogClient catalog, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var item = await db.Items.FirstOrDefaultAsync(i => i.UserId == userId && i.ProductId == productId, ct);
            if (item != null)
            {
                db.Items.Remove(item);
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(await LoadAndRefreshAsync(userId, db, catalog, ct));
        });

        g.MapDelete("/", async (HttpContext http, CartDb db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            await db.Items.Where(i => i.UserId == userId).ExecuteDeleteAsyncSafe(db, ct);
            return Results.NoContent();
        });
    }

    private static void Apply(CartItem item, CatalogProduct product, int quantity)
    {
        item.Quantity = quantity;
        item.Sku = product.Sku; item.Name = product.Name; item.ImageUrl = product.ImageUrl;
        item.UnitPrice = product.Price; // server-side price: any price sent by the client is ignored
    }

    private static async Task EnsureStockAsync(IInventoryClient inventory, CatalogProduct product, int quantity, CancellationToken ct)
    {
        var stock = (await inventory.GetStockAsync([product.Id], ct)).FirstOrDefault();
        var available = stock?.Available ?? 0;
        if (available < quantity)
            throw new ConflictException(available <= 0
                ? $"{product.Name} is out of stock."
                : $"Only {available} unit(s) of {product.Name} are available.");
    }

    /// <summary>Loads the cart, re-validates prices against the Catalog and drops products that are gone.</summary>
    public static async Task<CartDto> LoadAndRefreshAsync(Guid userId, CartDb db, ICatalogClient catalog, CancellationToken ct)
    {
        var items = await db.Items.Where(i => i.UserId == userId).OrderBy(i => i.AddedAtUtc).ToListAsync(ct);
        var warnings = new List<string>();
        if (items.Count > 0)
        {
            var products = (await catalog.GetProductsAsync(items.Select(i => i.ProductId), ct)).ToDictionary(p => p.Id);
            var changed = false;
            foreach (var item in items.ToList())
            {
                if (!products.TryGetValue(item.ProductId, out var p) || !p.IsActive)
                {
                    warnings.Add($"{item.Name} is no longer available and was removed from your cart.");
                    db.Items.Remove(item); items.Remove(item); changed = true; continue;
                }
                if (item.UnitPrice != p.Price)
                {
                    warnings.Add($"The price of {p.Name} changed from {item.UnitPrice:0.00} to {p.Price:0.00}.");
                    item.UnitPrice = p.Price; changed = true;
                }
                if (item.Name != p.Name || item.ImageUrl != p.ImageUrl) { item.Name = p.Name; item.ImageUrl = p.ImageUrl; changed = true; }
            }
            if (changed) await db.SaveChangesAsync(ct);
        }
        var dtos = items.Select(i => new CartItemDto(i.ProductId, i.Sku, i.Name, i.ImageUrl, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList();
        return new CartDto(dtos, dtos.Sum(i => i.Quantity), dtos.Sum(i => i.LineTotal), "USD", warnings);
    }
}

internal static class CartDbExtensions
{
    /// <summary>Deletes the matched rows with tracked entities (works for SQL Server and the InMemory test provider).</summary>
    public static async Task ExecuteDeleteAsyncSafe(this IQueryable<CartItem> query, CartDb db, CancellationToken ct)
    {
        var rows = await query.ToListAsync(ct);
        db.Items.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
