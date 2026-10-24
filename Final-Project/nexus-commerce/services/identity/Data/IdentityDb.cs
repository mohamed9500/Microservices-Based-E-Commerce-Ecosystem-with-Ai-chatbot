using Microsoft.EntityFrameworkCore;
using Nexus.Identity.Domain;

namespace Nexus.Identity.Data;

public sealed class IdentityDb(DbContextOptions<IdentityDb> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.AddressLine).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.PostalCode).HasMaxLength(20);
            e.Property(x => x.Country).HasMaxLength(100);
        });
    }
}
