using Microsoft.EntityFrameworkCore;
using Nexus.Contracts;
using Nexus.Inventory.Domain;

namespace Nexus.Inventory.Data;

public static class InventorySeeder
{
    /// <summary>Seeds stock for the 32 sample catalog products (ids match the catalog seed).</summary>
    public static async Task SeedAsync(InventoryDb db)
    {
        if (await db.Stock.AnyAsync()) return;
        for (var n = 1; n <= 32; n++)
        {
            var qty = n switch { 11 => 0, 23 => 3, _ => 12 + (n * 7) % 45 };
            db.Stock.Add(new StockItem { ProductId = SeedIds.Product(n), OnHand = qty });
            db.Adjustments.Add(new StockAdjustment { ProductId = SeedIds.Product(n), Delta = qty, QuantityAfter = qty, Reason = "Initial development seed", Source = "seed" });
        }
        await db.SaveChangesAsync();
    }
}
