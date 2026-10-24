using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Messaging;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts;
using Nexus.Contracts.Events;
using Nexus.Orders.Clients;
using Nexus.Orders.Data;
using Nexus.Orders.Domain;

namespace Nexus.Orders.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization("authenticated");

        // Place an order from the caller's server-side cart. Returns 202: confirmation happens asynchronously (saga).
        g.MapPost("/", async (CreateOrderRequest? req, HttpContext http, OrderDb db, ICartClient carts, IConfiguration config, CancellationToken ct) =>
        {
            req.Validated();
            req!.Shipping.Validated();
            if (!PaymentMethods.All.Contains(req.PaymentMethod))
                throw new ValidationFailedException("paymentMethod", $"Unsupported payment method. Allowed (mock only): {string.Join(", ", PaymentMethods.All)}.");

            var userId = http.User.GetUserId();
            var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
            if (key is { Length: > 100 }) throw new ValidationFailedException("Idempotency-Key", "Idempotency-Key must be at most 100 characters.");
            if (!string.IsNullOrEmpty(key))
            {
                var existing = await db.Orders.Include(o => o.Items).AsNoTracking()
                    .FirstOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == key, ct);
                if (existing != null) return Results.Ok(existing.ToDto()); // duplicate submit: return the original order
            }

            var token = http.Request.Headers.Authorization.ToString()["Bearer ".Length..];
            var cart = await carts.GetCartAsync(token, ct);
            if (cart.Items.Count == 0) throw new BadRequestException("Your cart is empty.");

            var subtotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
            var shipping = Pricing.ShippingFor(subtotal, config.GetValue("Shipping:FlatFee", 5m), config.GetValue("Shipping:FreeThreshold", 100m));
            var s = req.Shipping!;
            var order = new Order
            {
                UserId = userId, UserEmail = http.User.GetEmail(), CustomerName = http.User.GetDisplayName(),
                Subtotal = subtotal, ShippingFee = shipping, Total = subtotal + shipping, Currency = cart.Currency,
                PaymentMethod = req.PaymentMethod, IdempotencyKey = string.IsNullOrEmpty(key) ? null : key,
                CorrelationId = CorrelationContext.Current ?? Guid.NewGuid().ToString("N"),
                ShipName = s.FullName.Trim(), ShipLine1 = s.Line1.Trim(), ShipCity = s.City.Trim(),
                ShipPostalCode = s.PostalCode.Trim(), ShipCountry = s.Country.Trim(), ShipPhone = s.Phone.Trim()
            };
            foreach (var i in cart.Items)
                order.Items.Add(new OrderItem { ProductId = i.ProductId, Sku = i.Sku, Name = i.Name, ImageUrl = i.ImageUrl, UnitPrice = i.UnitPrice, Quantity = i.Quantity });

            db.Orders.Add(order);
            // Outbox row is committed atomically with the order: no lost or phantom OrderCreated events.
            db.EnqueueEvent(new OrderCreated(order.Id, userId, order.UserEmail,
                order.Items.Select(i => new OrderLine(i.ProductId, i.Sku, i.Name, i.Quantity, i.UnitPrice)).ToList(),
                order.Total, order.Currency, order.PaymentMethod));
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) when (!string.IsNullOrEmpty(key))
            {
                db.ChangeTracker.Clear();
                var existing = await db.Orders.Include(o => o.Items).AsNoTracking()
                    .FirstOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == key, ct);
                if (existing != null) return Results.Ok(existing.ToDto());
                throw;
            }
            return Results.Accepted($"/api/orders/{order.Id}", order.ToDto());
        });

        g.MapGet("/", async (HttpContext http, OrderDb db, int? page, int? pageSize, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var result = await db.Orders.AsNoTracking().Include(o => o.Items).Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 10, 50);
            return Results.Ok(result.Map(o => o.ToDto()));
        });

        g.MapGet("/{id:guid}", async (Guid id, HttpContext http, OrderDb db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, ct)
                        ?? throw new NotFoundException("Order not found.");
            return Results.Ok(order.ToDto());
        });

        g.MapPost("/{id:guid}/cancel", async (Guid id, HttpContext http, OrderDb db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId, ct)
                        ?? throw new NotFoundException("Order not found.");
            if (!OrderStateMachine.CustomerCanCancel(order.Status))
                throw new ConflictException($"An order that is {order.Status} can no longer be cancelled.");
            order.Status = OrderStatus.Cancelled;
            order.FailureReason = "Cancelled by customer";
            order.UpdatedAtUtc = DateTime.UtcNow;
            db.EnqueueEvent(new OrderCancelled(order.Id, order.UserId, order.UserEmail, order.FailureReason));
            await db.SaveChangesAsync(ct);
            return Results.Ok(order.ToDto());
        });

        // Used by the Review service (called with the customer's own token) to verify a purchase.
        g.MapGet("/has-purchased/{productId:guid}", async (Guid productId, HttpContext http, OrderDb db, CancellationToken ct) =>
        {
            var userId = http.User.GetUserId();
            var purchased = await db.Items.AnyAsync(i => i.ProductId == productId && i.Order.UserId == userId &&
                (i.Order.Status == OrderStatus.Confirmed || i.Order.Status == OrderStatus.Shipped || i.Order.Status == OrderStatus.Delivered), ct);
            return Results.Ok(new { purchased });
        });

        MapAdmin(app);
    }

    private static void MapAdmin(WebApplication app)
    {
        var g = app.MapGroup("/api/admin/orders").WithTags("Admin: Orders").RequireAuthorization("admin");

        g.MapGet("/", async (OrderDb db, string? status, string? q, int? page, int? pageSize, CancellationToken ct) =>
        {
            var query = db.Orders.AsNoTracking().Include(o => o.Items).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var st)) query = query.Where(o => o.Status == st);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(o => o.UserEmail.Contains(term) || o.CustomerName.Contains(term));
            }
            var result = await query.OrderByDescending(o => o.CreatedAtUtc).ToPagedAsync(page, pageSize, ct, 20, 100);
            return Results.Ok(result.Map(o => o.ToDto(true)));
        });

        g.MapGet("/{id:guid}", async (Guid id, OrderDb db, CancellationToken ct) =>
        {
            var order = await db.Orders.AsNoTracking().Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct)
                        ?? throw new NotFoundException("Order not found.");
            return Results.Ok(order.ToDto(true));
        });

        g.MapPut("/{id:guid}/status", async (Guid id, UpdateStatusRequest? req, OrderDb db, CancellationToken ct) =>
        {
            req.Validated();
            if (!Enum.TryParse<OrderStatus>(req!.Status, true, out var target) || target is not (OrderStatus.Shipped or OrderStatus.Delivered))
                throw new ValidationFailedException("status", "Admins may only move orders to Shipped or Delivered (payment and stock steps are automatic).");
            var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException("Order not found.");
            if (!OrderStateMachine.CanTransition(order.Status, target))
                throw new ConflictException($"Cannot move an order from {order.Status} to {target}.");
            order.Status = target;
            order.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(order.ToDto(true));
        });

        g.MapGet("/stats", async (OrderDb db, int? days, CancellationToken ct) =>
        {
            var span = Math.Clamp(days ?? 30, 1, 365);
            var from = DateTime.UtcNow.Date.AddDays(-(span - 1));
            var statusCounts = await db.Orders.AsNoTracking().GroupBy(o => o.Status).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
            var byStatus = Enum.GetValues<OrderStatus>().ToDictionary(s => s.ToString(), s => statusCounts.FirstOrDefault(c => c.Key == s)?.Count ?? 0);

            var revenueOrders = await db.Orders.AsNoTracking()
                .Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped || o.Status == OrderStatus.Delivered)
                .Select(o => new { o.CreatedAtUtc, o.Total }).ToListAsync(ct);
            var totalRevenue = revenueOrders.Sum(o => o.Total);
            var daily = Enumerable.Range(0, span).Select(i => from.AddDays(i)).Select(d =>
            {
                var day = revenueOrders.Where(o => o.CreatedAtUtc.Date == d).ToList();
                return new DailySales(d.ToString("yyyy-MM-dd"), day.Count, day.Sum(o => o.Total));
            }).ToList();

            var top = await db.Items.AsNoTracking()
                .Where(i => i.Order.Status == OrderStatus.Confirmed || i.Order.Status == OrderStatus.Shipped || i.Order.Status == OrderStatus.Delivered)
                .GroupBy(i => new { i.ProductId, i.Name })
                .Select(x => new { x.Key.ProductId, x.Key.Name, Quantity = x.Sum(i => i.Quantity), Revenue = x.Sum(i => i.UnitPrice * i.Quantity) })
                .OrderByDescending(t => t.Revenue).Take(5).ToListAsync(ct);
            var topProducts = top.Select(t => new TopProduct(t.ProductId, t.Name, t.Quantity, t.Revenue)).ToList();

            return Results.Ok(new OrderStats(byStatus.Values.Sum(), revenueOrders.Count, totalRevenue,
                revenueOrders.Count == 0 ? 0 : decimal.Round(totalRevenue / revenueOrders.Count, 2), byStatus, daily, topProducts));
        });
    }
}
