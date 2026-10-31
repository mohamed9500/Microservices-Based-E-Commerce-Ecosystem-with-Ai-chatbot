using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Notifications.Domain;

namespace Nexus.Notifications.Data;

public sealed class NotificationDb(DbContextOptions<NotificationDb> options) : DbContext(options), IMessagingDbContext
{
    public DbSet<NotificationRecord> Notifications { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<InboxMessage> InboxMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyMessagingModel();
        b.Entity<NotificationRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
            e.Property(x => x.Kind).HasMaxLength(40);
            e.Property(x => x.ToEmail).HasMaxLength(256);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Provider).HasMaxLength(20);
            e.Property(x => x.Error).HasMaxLength(500);
        });
    }
}
