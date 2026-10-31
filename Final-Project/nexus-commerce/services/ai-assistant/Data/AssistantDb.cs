using Microsoft.EntityFrameworkCore;
using Nexus.Assistant.Domain;

namespace Nexus.Assistant.Data;

public sealed class AssistantDb(DbContextOptions<AssistantDb> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations { get; set; } = null!;
    public DbSet<ChatMessage> Messages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Conversation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
            e.Property(x => x.Title).HasMaxLength(120);
            e.HasMany(x => x.Messages).WithOne(m => m.Conversation).HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ChatMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(12);
            e.Property(x => x.Content).HasMaxLength(6000);
            e.Property(x => x.Mode).HasMaxLength(12);
            e.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
        });
    }
}
