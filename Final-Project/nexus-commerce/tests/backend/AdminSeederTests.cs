using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Identity.Data;
using Nexus.Identity.Domain;
using Nexus.Identity.Services;

namespace Nexus.Tests;

public class AdminSeederTests
{
    [Theory]
    [InlineData("Development", true, true)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    public async Task Existing_admin_password_reset_requires_explicit_development_opt_in(
        string environmentName, bool resetEnabled, bool shouldReset)
    {
        var options = new DbContextOptionsBuilder<IdentityDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new IdentityDb(options);
        var hasher = new PasswordHasher<User>();
        var existing = new User
        {
            Email = "admin@test.local",
            NormalizedEmail = "ADMIN@TEST.LOCAL",
            FullName = "Store Administrator",
            Role = "Admin"
        };
        existing.PasswordHash = hasher.HashPassword(existing, "Original-Pass1");
        db.Users.Add(existing);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:Email"] = existing.Email,
            ["Admin:Password"] = "Updated-Pass2",
            ["Admin:ResetPassword"] = resetEnabled.ToString()
        }).Build();
        var environment = new TestHostEnvironment(environmentName);

        await AdminSeeder.SeedAsync(db, hasher, config, environment, NullLogger.Instance);

        var oldPasswordResult = hasher.VerifyHashedPassword(existing, existing.PasswordHash, "Original-Pass1");
        var newPasswordResult = hasher.VerifyHashedPassword(existing, existing.PasswordHash, "Updated-Pass2");
        Assert.Equal(shouldReset, newPasswordResult != PasswordVerificationResult.Failed);
        Assert.Equal(!shouldReset, oldPasswordResult != PasswordVerificationResult.Failed);
        Assert.Equal("Admin", existing.Role);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = nameof(AdminSeederTests);
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}