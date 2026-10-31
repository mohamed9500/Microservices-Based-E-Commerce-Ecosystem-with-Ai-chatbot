using Microsoft.EntityFrameworkCore;
using Nexus.Reviews.Domain;

namespace Nexus.Reviews.Data;

public sealed class ReviewDb(DbContextOptions<ReviewDb> options) : DbContext(options)
{
    public DbSet<Review> Reviews { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Review>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique(); // one review per customer per product
            e.HasIndex(x => new { x.ProductId, x.IsVisible, x.CreatedAtUtc });
            e.Property(x => x.UserName).HasMaxLength(150);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(2000);
        });
    }
}
