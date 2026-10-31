using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Notifications.Data;

namespace Nexus.Notifications.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/notifications", async (HttpContext http, NotificationDb db, int? page, int? pageSize, CancellationToken ct) =>
        {
            var uid = http.User.GetUserId();
            return Results.Ok(await db.Notifications.AsNoTracking().Where(n => n.UserId == uid)
                .OrderByDescending(n => n.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 20, 50));
        }).WithTags("Notifications").RequireAuthorization("authenticated");

        app.MapGet("/api/admin/notifications", async (NotificationDb db, int? page, int? pageSize, CancellationToken ct) =>
            Results.Ok(await db.Notifications.AsNoTracking().OrderByDescending(n => n.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 20, 100)))
            .WithTags("Admin: Notifications").RequireAuthorization("admin");
    }
}
