using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;
using Nexus.Reviews.Clients;
using Nexus.Reviews.Data;
using Nexus.Reviews.Domain;

namespace Nexus.Reviews.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/reviews").WithTags("Reviews");

        g.MapGet("/product/{productId:guid}", async (Guid productId, ReviewDb db, int? page, int? pageSize, CancellationToken ct) =>
        {
            var result = await db.Reviews.AsNoTracking().Where(r => r.ProductId == productId && r.IsVisible)
                .OrderByDescending(r => r.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 10, 50);
            return Results.Ok(result.Map(r => r.ToDto()));
        });

        g.MapGet("/summary", async (string ids, ReviewDb db, CancellationToken ct) =>
        {
            var parsed = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var x) ? x : (Guid?)null).Where(x => x.HasValue).Select(x => x!.Value).Distinct().Take(100).ToList();
            var rows = await db.Reviews.AsNoTracking().Where(r => r.IsVisible && parsed.Contains(r.ProductId))
                .GroupBy(r => r.ProductId).Select(x => new { ProductId = x.Key, Avg = x.Average(r => (double)r.Rating), Count = x.Count() }).ToListAsync(ct);
            return Results.Ok(parsed.Select(id =>
            {
                var row = rows.FirstOrDefault(r => r.ProductId == id);
                return new RatingSummary(id, row == null ? 0 : Math.Round(row.Avg, 1), row?.Count ?? 0);
            }).ToList());
        });

        g.MapPost("/", async (CreateReviewRequest? req, HttpContext http, ReviewDb db, IOrdersClient orders, CancellationToken ct) =>
        {
            req.Validated();
            var userId = http.User.GetUserId();
            if (await db.Reviews.AnyAsync(r => r.ProductId == req!.ProductId && r.UserId == userId, ct))
                throw new ConflictException("You have already reviewed this product.");
            var token = http.Request.Headers.Authorization.ToString()["Bearer ".Length..];
            if (!await orders.HasPurchasedAsync(token, req!.ProductId, ct))
                throw new ForbiddenException("Only customers who purchased this product can review it.");

            var review = new Review
            {
                ProductId = req.ProductId, UserId = userId, UserName = http.User.GetDisplayName(), Rating = req.Rating,
                Title = req.Title.Trim(), Body = req.Body.Trim(), VerifiedPurchase = true
            };
            db.Reviews.Add(review);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { throw new ConflictException("You have already reviewed this product."); }
            return Results.Created($"/api/reviews/{review.Id}", review.ToDto());
        }).RequireAuthorization("authenticated");

        g.MapDelete("/{id:guid}", async (Guid id, HttpContext http, ReviewDb db, CancellationToken ct) =>
        {
            var r = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Review not found.");
            if (r.UserId != http.User.GetUserId() && !http.User.IsAdmin()) throw new NotFoundException("Review not found.");
            db.Reviews.Remove(r);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization("authenticated");

        var admin = app.MapGroup("/api/admin/reviews").WithTags("Admin: Reviews").RequireAuthorization("admin");
        admin.MapGet("/", async (ReviewDb db, bool? visible, int? page, int? pageSize, CancellationToken ct) =>
        {
            var q = db.Reviews.AsNoTracking().AsQueryable();
            if (visible.HasValue) q = q.Where(r => r.IsVisible == visible.Value);
            var result = await q.OrderByDescending(r => r.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 20, 100);
            return Results.Ok(result.Map(r => r.ToDto(true)));
        });
        admin.MapPut("/{id:guid}/visibility", async (Guid id, VisibilityRequest? req, ReviewDb db, CancellationToken ct) =>
        {
            req.Validated();
            var r = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Review not found.");
            r.IsVisible = req!.IsVisible;
            await db.SaveChangesAsync(ct);
            return Results.Ok(r.ToDto(true));
        });
    }
}
