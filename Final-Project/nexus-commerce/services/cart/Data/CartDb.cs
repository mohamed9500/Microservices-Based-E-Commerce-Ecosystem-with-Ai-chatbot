using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Cart.Domain;

namespace Nexus.Cart.Data;

public sealed class CartDb(DbContextOptions<CartDb> options) : DbContext(options), IMessagingDbContext
{
    public DbSet<CartItem> Items { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyMessagingModel();
        b.Entity<CartItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique();
            e.Property(x => x.Sku).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
        });
    }
}
