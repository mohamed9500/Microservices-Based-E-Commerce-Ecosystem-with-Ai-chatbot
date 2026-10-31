using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Contracts.Events;
using Nexus.Inventory.Data;
using Nexus.Inventory.Domain;

namespace Nexus.Inventory.Messaging;

/// <summary>
/// Stock reservation rules (reserve -> commit | release). All state changes plus the resulting outbox event are
/// persisted by one SaveChanges (done by the consumer framework), and row-version concurrency tokens make concurrent
/// reservations of the same product fail-and-retry instead of overselling.
/// </summary>
public sealed class ReservationService(InventoryDb db)
{
    public async Task ReserveAsync(OrderCreated evt, CancellationToken ct)
    {
        // Idempotency on business level as well (in addition to the message inbox).
        if (await db.Reservations.AnyAsync(r => r.OrderId == evt.OrderId, ct)) return;

        var need = evt.Items.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var ids = need.Keys.ToList();
        var stock = await db.Stock.Where(s => ids.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId, ct);

        var shortages = need.Where(n => !stock.TryGetValue(n.Key, out var s) || s.OnHand - s.Reserved < n.Value)
            .Select(n => n.Key).ToList();
        if (shortages.Count > 0)
        {
            var names = evt.Items.Where(i => shortages.Contains(i.ProductId)).Select(i => i.Name).Distinct();
            db.EnqueueEvent(new InventoryReservationFailed(evt.OrderId, $"Insufficient stock for: {string.Join(", ", names)}"));
            return;
        }

        foreach (var (productId, qty) in need)
        {
            stock[productId].Reserved += qty;
            stock[productId].UpdatedAtUtc = DateTime.UtcNow;
            db.Reservations.Add(new StockReservation { OrderId = evt.OrderId, ProductId = productId, Quantity = qty });
        }
        db.EnqueueEvent(new InventoryReserved(evt.OrderId, evt.UserId, evt.Total, evt.Currency, evt.PaymentMethod));
    }

    /// <summary>Order confirmed: the reserved units leave the shelf.</summary>
    public async Task CommitAsync(Guid orderId, CancellationToken ct)
    {
        var reservations = await db.Reservations.Where(r => r.OrderId == orderId && r.Status == ReservationStatus.Reserved).ToListAsync(ct);
        if (reservations.Count == 0) return;
        var ids = reservations.Select(r => r.ProductId).ToList();
        var stock = await db.Stock.Where(s => ids.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId, ct);
        foreach (var r in reservations)
        {
            var s = stock[r.ProductId];
            s.Reserved -= r.Quantity;
            s.OnHand -= r.Quantity;
            s.UpdatedAtUtc = DateTime.UtcNow;
            r.Status = ReservationStatus.Committed;
            r.UpdatedAtUtc = DateTime.UtcNow;
            db.Adjustments.Add(new StockAdjustment { ProductId = r.ProductId, Delta = -r.Quantity, QuantityAfter = s.OnHand, Reason = "Order confirmed", Source = "order", OrderId = orderId });
        }
    }

    /// <summary>Order cancelled: free reserved units, or restock units that were already committed.</summary>
    public async Task ReleaseAsync(Guid orderId, CancellationToken ct)
    {
        var reservations = await db.Reservations
            .Where(r => r.OrderId == orderId && r.Status != ReservationStatus.Released).ToListAsync(ct);
        if (reservations.Count == 0) return;
        var ids = reservations.Select(r => r.ProductId).ToList();
        var stock = await db.Stock.Where(s => ids.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId, ct);
        foreach (var r in reservations)
        {
            var s = stock[r.ProductId];
            if (r.Status == ReservationStatus.Reserved)
            {
                s.Reserved -= r.Quantity;
            }
            else // Committed: units already deducted from OnHand are returned
            {
                s.OnHand += r.Quantity;
                db.Adjustments.Add(new StockAdjustment { ProductId = r.ProductId, Delta = r.Quantity, QuantityAfter = s.OnHand, Reason = "Order cancelled - restocked", Source = "order-cancel", OrderId = orderId });
            }
            s.UpdatedAtUtc = DateTime.UtcNow;
            r.Status = ReservationStatus.Released;
            r.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
