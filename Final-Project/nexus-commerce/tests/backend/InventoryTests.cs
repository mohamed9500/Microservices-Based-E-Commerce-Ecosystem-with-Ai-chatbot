using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Contracts.Events;
using Nexus.Inventory.Data;
using Nexus.Inventory.Domain;
using Nexus.Inventory.Messaging;

namespace Nexus.Tests;

public class InventoryTests
{
    private static InventoryDb NewDb() =>
        new(new DbContextOptionsBuilder<InventoryDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static OrderCreated Order(Guid orderId, Guid productId, int qty) =>
        new(orderId, Guid.NewGuid(), "c@test.local", [new OrderLine(productId, "SKU", "Widget", qty, 10m)], 10m * qty, "USD", "mock_success");

    private static async Task<(InventoryDb db, Guid product)> WithStock(int onHand)
    {
        var db = NewDb();
        var product = Guid.NewGuid();
        db.Stock.Add(new StockItem { ProductId = product, OnHand = onHand });
        await db.SaveChangesAsync();
        return (db, product);
    }

    [Fact]
    public async Task Reserve_succeeds_and_emits_InventoryReserved()
    {
        var (db, product) = await WithStock(5);
        await new ReservationService(db).ReserveAsync(Order(Guid.NewGuid(), product, 3), CancellationToken.None);
        await db.SaveChangesAsync();
        var stock = await db.Stock.SingleAsync();
        Assert.Equal(3, stock.Reserved);
        Assert.Equal(2, stock.Available);
        Assert.Contains(db.OutboxMessages, m => m.RoutingKey == "inventory.reserved.v1");
    }

    [Fact]
    public async Task Overselling_is_prevented_and_failure_event_emitted()
    {
        var (db, product) = await WithStock(2);
        var svc = new ReservationService(db);
        await svc.ReserveAsync(Order(Guid.NewGuid(), product, 2), CancellationToken.None);
        await db.SaveChangesAsync();
        await svc.ReserveAsync(Order(Guid.NewGuid(), product, 1), CancellationToken.None); // nothing left
        await db.SaveChangesAsync();
        Assert.Equal(2, (await db.Stock.SingleAsync()).Reserved);
        Assert.Contains(db.OutboxMessages, m => m.RoutingKey == "inventory.reservation-failed.v1");
    }

    [Fact]
    public async Task Reserving_the_same_order_twice_is_idempotent()
    {
        var (db, product) = await WithStock(10);
        var svc = new ReservationService(db);
        var order = Order(Guid.NewGuid(), product, 4);
        await svc.ReserveAsync(order, CancellationToken.None); await db.SaveChangesAsync();
        await svc.ReserveAsync(order, CancellationToken.None); await db.SaveChangesAsync();
        Assert.Equal(4, (await db.Stock.SingleAsync()).Reserved);
        Assert.Single(db.Reservations);
    }

    [Fact]
    public async Task Commit_removes_units_from_shelf_and_records_history()
    {
        var (db, product) = await WithStock(10);
        var svc = new ReservationService(db);
        var id = Guid.NewGuid();
        await svc.ReserveAsync(Order(id, product, 4), CancellationToken.None); await db.SaveChangesAsync();
        await svc.CommitAsync(id, CancellationToken.None); await db.SaveChangesAsync();
        var s = await db.Stock.SingleAsync();
        Assert.Equal((6, 0), (s.OnHand, s.Reserved));
        Assert.Contains(db.Adjustments, a => a.Delta == -4 && a.Source == "order");
        // committing again changes nothing
        await svc.CommitAsync(id, CancellationToken.None); await db.SaveChangesAsync();
        Assert.Equal(6, (await db.Stock.SingleAsync()).OnHand);
    }

    [Fact]
    public async Task Release_before_commit_frees_the_reservation()
    {
        var (db, product) = await WithStock(10);
        var svc = new ReservationService(db);
        var id = Guid.NewGuid();
        await svc.ReserveAsync(Order(id, product, 4), CancellationToken.None); await db.SaveChangesAsync();
        await svc.ReleaseAsync(id, CancellationToken.None); await db.SaveChangesAsync();
        var s = await db.Stock.SingleAsync();
        Assert.Equal((10, 0), (s.OnHand, s.Reserved));
    }

    [Fact]
    public async Task Release_after_commit_restocks()
    {
        var (db, product) = await WithStock(10);
        var svc = new ReservationService(db);
        var id = Guid.NewGuid();
        await svc.ReserveAsync(Order(id, product, 4), CancellationToken.None); await db.SaveChangesAsync();
        await svc.CommitAsync(id, CancellationToken.None); await db.SaveChangesAsync();
        await svc.ReleaseAsync(id, CancellationToken.None); await db.SaveChangesAsync();
        Assert.Equal(10, (await db.Stock.SingleAsync()).OnHand);
    }

    [Fact]
    public async Task Unknown_product_cannot_be_reserved()
    {
        var db = NewDb();
        await new ReservationService(db).ReserveAsync(Order(Guid.NewGuid(), Guid.NewGuid(), 1), CancellationToken.None);
        await db.SaveChangesAsync();
        Assert.Empty(db.Reservations);
        Assert.Contains(db.OutboxMessages, m => m.RoutingKey == "inventory.reservation-failed.v1");
    }
}
