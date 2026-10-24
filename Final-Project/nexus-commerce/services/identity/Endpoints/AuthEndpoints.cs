using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Identity.Data;
using Nexus.Identity.Domain;
using Nexus.Identity.Services;

namespace Nexus.Identity.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapPost("/register", async (RegisterRequest? req, IdentityDb db, IPasswordHasher<User> hasher, TokenService tokens, CancellationToken ct) =>
        {
            req.Validated();
            EnsureStrongPassword(req!.Password);
            var email = req.Email.Trim();
            var normalized = email.ToUpperInvariant();
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, ct))
                throw new ConflictException("An account with this email already exists.");

            var user = new User { Email = email, NormalizedEmail = normalized, FullName = req.FullName.Trim(), Role = Roles.Customer };
            user.PasswordHash = hasher.HashPassword(user, req.Password);
            db.Users.Add(user);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { throw new ConflictException("An account with this email already exists."); }

            var (token, exp) = tokens.Create(user);
            return Results.Created($"/api/profile", new AuthResponse(token, exp, user.ToDto()));
        });

        g.MapPost("/login", async (LoginRequest? req, IdentityDb db, IPasswordHasher<User> hasher, TokenService tokens, CancellationToken ct) =>
        {
            req.Validated();
            var normalized = req!.Email.Trim().ToUpperInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
            // Same response for unknown user / wrong password / disabled account (no account enumeration).
            if (user == null || !user.IsActive ||
                hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
                throw new UnauthorizedException("Invalid email or password.");

            user.LastLoginAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var (token, exp) = tokens.Create(user);
            return Results.Ok(new AuthResponse(token, exp, user.ToDto()));
        });
    }

    internal static void EnsureStrongPassword(string password)
    {
        if (!Regex.IsMatch(password, "[A-Za-z]") || !Regex.IsMatch(password, "[0-9]"))
            throw new ValidationFailedException("password", "Password must contain at least one letter and one digit.");
    }
}
