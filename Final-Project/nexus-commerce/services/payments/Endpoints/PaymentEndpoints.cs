using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Payments.Data;
using Nexus.Payments.Domain;

namespace Nexus.Payments.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/payments/order/{orderId:guid}", async (Guid orderId, HttpContext http, PaymentDb db, CancellationToken ct) =>
        {
            var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
            // Owners and admins only; everyone else gets 404 (no information leak).
            if (p == null || (p.UserId != http.User.GetUserId() && !http.User.IsAdmin())) throw new NotFoundException("Payment not found.");
            return Results.Ok(p.ToDto());
        }).WithTags("Payments").RequireAuthorization("authenticated");

        app.MapGet("/api/admin/payments", async (PaymentDb db, string? status, int? page, int? pageSize, CancellationToken ct) =>
        {
            var q = db.Payments.AsNoTracking().AsQueryable();
            if (Enum.TryParse<PaymentStatus>(status, true, out var s)) q = q.Where(p => p.Status == s);
            var result = await q.OrderByDescending(p => p.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 20, 100);
            return Results.Ok(result.Map(p => p.ToDto()));
        }).WithTags("Admin: Payments").RequireAuthorization("admin");
    }
}
