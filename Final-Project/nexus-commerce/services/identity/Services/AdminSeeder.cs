using Microsoft.AspNetCore.Identity;
using Nexus.BuildingBlocks.Auth;
using Nexus.Identity.Data;
using Nexus.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Nexus.Identity.Services;

/// <summary>
/// Development bootstrap: creates (or re-promotes) the administrator described by Admin__Email / Admin__Password.
/// Nothing is hard-coded; with no values configured no admin is created. Outside Development it additionally
/// requires Admin__AllowOutsideDevelopment=true and logs a warning.
/// </summary>
public static class AdminSeeder
{
    public static async Task SeedAsync(IdentityDb db, IPasswordHasher<User> hasher, IConfiguration config,
        IHostEnvironment env, ILogger logger)
    {
        var email = config["Admin:Email"]?.Trim();
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation("No Admin:Email/Admin:Password configured; skipping administrator bootstrap.");
            return;
        }
        if (!env.IsDevelopment() && !config.GetValue<bool>("Admin:AllowOutsideDevelopment"))
        {
            logger.LogWarning("Administrator bootstrap ignored: not Development and Admin:AllowOutsideDevelopment is not true.");
            return;
        }
        var resetPassword = config.GetValue<bool>("Admin:ResetPassword");
        if (resetPassword && !env.IsDevelopment())
        {
            logger.LogWarning("Administrator password reset ignored outside Development.");
            resetPassword = false;
        }
        if (password.Length < 8)
        {
            logger.LogError("Admin:Password must be at least 8 characters; administrator not created.");
            return;
        }

        var normalized = email.ToUpperInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized);
        if (user == null)
        {
            user = new User { Email = email, NormalizedEmail = normalized, FullName = config["Admin:FullName"] ?? "Store Administrator", Role = Roles.Admin };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            logger.LogWarning("Created administrator account {Email} from configuration (development bootstrap).", email);
        }
        else
        {
            if (user.Role != Roles.Admin)
            {
                user.Role = Roles.Admin;
                logger.LogWarning("Promoted existing user {Email} to Admin from configuration.", email);
            }
            if (resetPassword)
            {
                user.PasswordHash = hasher.HashPassword(user, password);
                logger.LogWarning("Reset administrator password for {Email} using the explicit Development-only setting.", email);
            }
        }
        await db.SaveChangesAsync();
    }
}
