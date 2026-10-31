using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Inventory.Data;
using Nexus.Inventory.Domain;

namespace Nexus.Inventory.Endpoints;

public static class InventoryEndpoints
{
    public const int LowStockThreshold = 5;

    public static string StatusFor(int available) =>
        available <= 0 ? "out_of_stock" : available <= LowStockThreshold ? "low_stock" : "in_stock";

    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/inventory/stock", async (string ids, InventoryDb db, CancellationToken ct) =>
        {
            var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null).Where(g => g.HasValue).Select(g => g!.Value)
                .Distinct().Take(100).ToList();
            var found = await db.Stock.AsNoTracking().Where(s => parsed.Contains(s.ProductId)).ToListAsync(ct);
            var map = found.ToDictionary(s => s.ProductId, s => Math.Max(0, s.OnHand - s.Reserved));
            // Unknown products are reported as out of stock rather than failing.
            return Results.Ok(parsed.Select(id => { var a = map.GetValueOrDefault(id); return new StockDto(id, a, StatusFor(a)); }).ToList());
        }).WithTags("Inventory");

        var g = app.MapGroup("/api/admin/inventory").WithTags("Admin: Inventory").RequireAuthorization("admin");

        g.MapGet("/", async (InventoryDb db, int? page, int? pageSize, bool? lowOnly, CancellationToken ct) =>
        {
            var q = db.Stock.AsNoTracking().AsQueryable();
            if (lowOnly == true) q = q.Where(s => s.OnHand - s.Reserved <= LowStockThreshold);
            var result = await q.OrderBy(s => s.OnHand - s.Reserved).ThenBy(s => s.ProductId).ToPagedAsync(page, pageSize, ct, 50, 200);
            return Results.Ok(result.Map(s => new AdminStockDto(s.ProductId, s.OnHand, s.Reserved, s.Available, s.UpdatedAtUtc)));
        });

        g.MapPost("/{productId:guid}/adjust", async (Guid productId, AdjustRequest? req, HttpContext http, InventoryDb db, CancellationToken ct) =>
        {
            req.Validated();
            var item = await GetOrCreateAsync(db, productId, ct);
            return await ApplyAsync(db, item, item.OnHand + req!.Delta, req.Reason, http, ct);
        });

        g.MapPut("/{productId:guid}", async (Guid productId, SetStockRequest? req, HttpContext http, InventoryDb db, CancellationToken ct) =>
        {
            req.Validated();
            var item = await GetOrCreateAsync(db, productId, ct);
            return await ApplyAsync(db, item, req!.OnHand, req.Reason, http, ct);
        });

        g.MapGet("/{productId:guid}/history", async (Guid productId, InventoryDb db, int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await db.Adjustments.AsNoTracking().Where(a => a.ProductId == productId)
                .OrderByDescending(a => a.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 20, 100)));
    }

    private static async Task<StockItem> GetOrCreateAsync(InventoryDb db, Guid productId, CancellationToken ct)
    {
        var item = await db.Stock.FirstOrDefaultAsync(s => s.ProductId == productId, ct);
        if (item != null) return item;
        item = new StockItem { ProductId = productId };
        db.Stock.Add(item);
        return item;
    }

    private static async Task<IResult> ApplyAsync(InventoryDb db, StockItem item, int newOnHand, string reason, HttpContext http, CancellationToken ct)
    {
        if (newOnHand < 0) throw new ValidationFailedException("delta", "Stock cannot become negative.");
        if (newOnHand < item.Reserved)
            throw new ConflictException($"{item.Reserved} units are reserved by open orders; stock cannot go below that.");
        var delta = newOnHand - item.OnHand;
        if (delta == 0) return Results.Ok(new AdminStockDto(item.ProductId, item.OnHand, item.Reserved, item.Available, item.UpdatedAtUtc));
        item.OnHand = newOnHand;
        item.UpdatedAtUtc = DateTime.UtcNow;
        db.Adjustments.Add(new StockAdjustment
        {
            ProductId = item.ProductId, Delta = delta, QuantityAfter = newOnHand, Reason = reason.Trim(),
            Source = "admin", ActorUserId = http.User.GetUserId()
        });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new AdminStockDto(item.ProductId, item.OnHand, item.Reserved, item.Available, item.UpdatedAtUtc));
    }
}
