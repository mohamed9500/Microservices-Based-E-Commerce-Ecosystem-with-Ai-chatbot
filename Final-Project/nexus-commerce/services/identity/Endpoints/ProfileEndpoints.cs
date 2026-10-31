using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Identity.Data;
using Nexus.Identity.Domain;

namespace Nexus.Identity.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/profile").WithTags("Profile").RequireAuthorization("authenticated");

        g.MapGet("/", async (HttpContext http, IdentityDb db, CancellationToken ct) =>
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == http.User.GetUserId(), ct)
                       ?? throw new NotFoundException("Account not found.");
            return Results.Ok(user.ToDto());
        });

        g.MapPut("/", async (UpdateProfileRequest? req, HttpContext http, IdentityDb db, CancellationToken ct) =>
        {
            req.Validated();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == http.User.GetUserId(), ct)
                       ?? throw new NotFoundException("Account not found.");
            user.FullName = req!.FullName.Trim();
            user.Phone = req.Phone?.Trim();
            user.AddressLine = req.AddressLine?.Trim();
            user.City = req.City?.Trim();
            user.PostalCode = req.PostalCode?.Trim();
            user.Country = req.Country?.Trim();
            await db.SaveChangesAsync(ct);
            return Results.Ok(user.ToDto());
        });

        g.MapPost("/change-password", async (ChangePasswordRequest? req, HttpContext http, IdentityDb db, IPasswordHasher<User> hasher, CancellationToken ct) =>
        {
            req.Validated();
            AuthEndpoints.EnsureStrongPassword(req!.NewPassword);
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == http.User.GetUserId(), ct)
                       ?? throw new NotFoundException("Account not found.");
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
                throw new ValidationFailedException("currentPassword", "Current password is incorrect.");
            user.PasswordHash = hasher.HashPassword(user, req.NewPassword);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
