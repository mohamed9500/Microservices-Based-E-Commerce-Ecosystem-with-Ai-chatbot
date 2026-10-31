using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Payments.Domain;

namespace Nexus.Payments.Data;

public sealed class PaymentDb(DbContextOptions<PaymentDb> options) : DbContext(options), IMessagingDbContext
{
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyMessagingModel();
        b.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrderId).IsUnique(); // one payment intent per order
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Provider).HasMaxLength(30);
            e.Property(x => x.ProviderReference).HasMaxLength(100);
            e.Property(x => x.FailureReason).HasMaxLength(300);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });
    }
}
