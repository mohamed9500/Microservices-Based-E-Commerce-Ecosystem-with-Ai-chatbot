using Microsoft.EntityFrameworkCore;
using Nexus.Catalog.Domain;

namespace Nexus.Catalog.Data;

public sealed class CatalogDb(DbContextOptions<CatalogDb> options) : DbContext(options)
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Category>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
        });
        b.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(220).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.Brand).HasMaxLength(100).IsRequired();
            e.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.CategoryId, x.IsActive });
            e.HasIndex(x => x.Price);
            e.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
