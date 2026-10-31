using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Contracts.Events;
using Nexus.Orders.Data;
using Nexus.Orders.Domain;

namespace Nexus.Orders.Messaging;

/// <summary>
/// Order saga (choreography). Pending -> AwaitingPayment (stock reserved) -> Confirmed (payment authorized),
/// or Cancelled on stock/payment failure. Handlers ignore events that no longer apply (e.g. customer cancelled
/// while payment was in flight) so late or duplicate messages are harmless.
/// </summary>
public sealed class OrderSagaHandlers(OrderDb db, ILogger<OrderSagaHandlers> logger)
{
    public async Task OnInventoryReservedAsync(InventoryReserved e, CancellationToken ct)
    {
        var order = await FindAsync(e.OrderId, ct);
        if (order == null) return;
        if (order.Status == OrderStatus.Pending) Move(order, OrderStatus.AwaitingPayment);
        else if (order.Status == OrderStatus.Cancelled)
            // Customer cancelled before the reservation arrived: release the stock again.
            db.EnqueueEvent(new OrderCancelled(order.Id, order.UserId, order.UserEmail, order.FailureReason ?? "Cancelled"));
    }

    public async Task OnInventoryFailedAsync(InventoryReservationFailed e, CancellationToken ct)
    {
        var order = await FindAsync(e.OrderId, ct);
        if (order is not { Status: OrderStatus.Pending }) return;
        order.FailureReason = e.Reason;
        Move(order, OrderStatus.Cancelled);
        // No stock was reserved, but notify (customer email); inventory release is a no-op.
        db.EnqueueEvent(new OrderCancelled(order.Id, order.UserId, order.UserEmail, e.Reason));
    }

    public async Task OnPaymentAuthorizedAsync(PaymentAuthorized e, CancellationToken ct)
    {
        var order = await FindAsync(e.OrderId, ct);
        if (order == null) return;
        if (order.Status is not (OrderStatus.Pending or OrderStatus.AwaitingPayment))
        {
            logger.LogWarning("PaymentAuthorized for order {OrderId} in status {Status} ignored (payment will be voided on cancel).", order.Id, order.Status);
            return;
        }
        Move(order, OrderStatus.Confirmed);
        db.EnqueueEvent(new OrderConfirmed(order.Id, order.UserId, order.UserEmail, order.Total, order.Currency,
            order.Items.Select(i => new OrderLine(i.ProductId, i.Sku, i.Name, i.Quantity, i.UnitPrice)).ToList()));
    }

    public async Task OnPaymentFailedAsync(PaymentFailed e, CancellationToken ct)
    {
        var order = await FindAsync(e.OrderId, ct);
        if (order == null || order.Status is not (OrderStatus.Pending or OrderStatus.AwaitingPayment)) return;
        order.FailureReason = $"Payment failed: {e.Reason}";
        Move(order, OrderStatus.Cancelled);
        db.EnqueueEvent(new OrderCancelled(order.Id, order.UserId, order.UserEmail, order.FailureReason));
    }

    private Task<Order?> FindAsync(Guid id, CancellationToken ct) =>
        db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    private void Move(Order order, OrderStatus to)
    {
        if (!OrderStateMachine.CanTransition(order.Status, to)) return;
        logger.LogInformation("Order {OrderId}: {From} -> {To}", order.Id, order.Status, to);
        order.Status = to;
        order.UpdatedAtUtc = DateTime.UtcNow;
    }
}
