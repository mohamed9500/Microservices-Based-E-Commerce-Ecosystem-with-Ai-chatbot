using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Orders.Domain;

namespace Nexus.Orders.Data;

public sealed class OrderDb(DbContextOptions<OrderDb> options) : DbContext(options), IMessagingDbContext
{
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> Items { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyMessagingModel();
        b.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.ShippingFee).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.UserEmail).HasMaxLength(256);
            e.Property(x => x.CustomerName).HasMaxLength(150);
            e.Property(x => x.PaymentMethod).HasMaxLength(30);
            e.Property(x => x.ShipName).HasMaxLength(150);
            e.Property(x => x.ShipLine1).HasMaxLength(200);
            e.Property(x => x.ShipCity).HasMaxLength(100);
            e.Property(x => x.ShipPostalCode).HasMaxLength(20);
            e.Property(x => x.ShipCountry).HasMaxLength(100);
            e.Property(x => x.ShipPhone).HasMaxLength(30);
            e.Property(x => x.IdempotencyKey).HasMaxLength(100);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            e.Property(x => x.FailureReason).HasMaxLength(500);
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            e.HasMany(x => x.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OrderItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasIndex(x => x.ProductId);
        });
    }
}
