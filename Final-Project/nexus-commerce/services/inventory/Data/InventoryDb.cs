using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Inventory.Domain;

namespace Nexus.Inventory.Data;

public sealed class InventoryDb(DbContextOptions<InventoryDb> options) : DbContext(options), IMessagingDbContext
{
    public DbSet<StockItem> Stock { get; set; } = null!;
    public DbSet<StockReservation> Reservations { get; set; } = null!;
    public DbSet<StockAdjustment> Adjustments { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyMessagingModel();
        b.Entity<StockItem>(e =>
        {
            e.HasKey(x => x.ProductId);
            e.Ignore(x => x.Available);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t => t.HasCheckConstraint("CK_Stock_NonNegative", "[OnHand] >= 0 AND [Reserved] >= 0 AND [Reserved] <= [OnHand]"));
        });
        b.Entity<StockReservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
        b.Entity<StockAdjustment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProductId, x.CreatedAtUtc });
            e.Property(x => x.Reason).HasMaxLength(200);
            e.Property(x => x.Source).HasMaxLength(20);
        });
    }
}
