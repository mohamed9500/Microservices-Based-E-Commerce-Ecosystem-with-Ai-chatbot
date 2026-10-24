namespace Nexus.Contracts.Events;

public sealed record OrderLine(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice);

/// <summary>Published by Orders when a customer places an order (saga start).</summary>
[EventContract("order.created", 1)]
public sealed record OrderCreated(
    Guid OrderId, Guid UserId, string UserEmail,
    IReadOnlyList<OrderLine> Items, decimal Total, string Currency, string PaymentMethod);

/// <summary>Published by Orders once payment is authorized.</summary>
[EventContract("order.confirmed", 1)]
public sealed record OrderConfirmed(
    Guid OrderId, Guid UserId, string UserEmail, decimal Total, string Currency,
    IReadOnlyList<OrderLine> Items);

/// <summary>Published by Orders when an order is cancelled (customer, payment failure, ...).</summary>
[EventContract("order.cancelled", 1)]
public sealed record OrderCancelled(Guid OrderId, Guid UserId, string UserEmail, string Reason);
