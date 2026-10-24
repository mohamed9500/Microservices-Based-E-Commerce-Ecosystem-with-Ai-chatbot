using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Identity.Data;
using Nexus.Identity.Domain;

namespace Nexus.Identity.Endpoints;

public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/admin/users").WithTags("Admin: Users").RequireAuthorization("admin");

        g.MapGet("/", async (IdentityDb db, string? q, int? page, int? pageSize, CancellationToken ct) =>
        {
            var query = db.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(u => u.Email.Contains(term) || u.FullName.Contains(term));
            }
            var result = await query.OrderByDescending(u => u.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 20, 100);
            return Results.Ok(result.Map(u => u.ToDto()));
        });

        g.MapPut("/{id:guid}/active", async (Guid id, SetActiveRequest? req, HttpContext http, IdentityDb db, CancellationToken ct) =>
        {
            req.Validated();
            if (id == http.User.GetUserId()) throw new BadRequestException("You cannot change the active state of your own account.");
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException("User not found.");
            user.IsActive = req!.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(user.ToDto());
        });
    }
}
