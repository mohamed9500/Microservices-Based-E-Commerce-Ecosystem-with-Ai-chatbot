namespace Nexus.Orders.Domain;

public enum OrderStatus { Pending, AwaitingPayment, Confirmed, Shipped, Delivered, Cancelled }

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "";
    public string ShipName { get; set; } = "";
    public string ShipLine1 { get; set; } = "";
    public string ShipCity { get; set; } = "";
    public string ShipPostalCode { get; set; } = "";
    public string ShipCountry { get; set; } = "";
    public string ShipPhone { get; set; } = "";
    public string? IdempotencyKey { get; set; }
    public string CorrelationId { get; set; } = "";
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = [];
}

/// <summary>Immutable snapshot of what was bought, at the price that was charged.</summary>
public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Pure rules for the order status lifecycle (unit tested).</summary>
public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.AwaitingPayment, OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.AwaitingPayment] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);
    public static bool CustomerCanCancel(OrderStatus s) => s is OrderStatus.Pending or OrderStatus.AwaitingPayment or OrderStatus.Confirmed;
    public static bool CountsAsRevenue(OrderStatus s) => s is OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered;
}

public static class Pricing
{
    public static decimal ShippingFor(decimal subtotal, decimal flatFee, decimal freeThreshold) =>
        subtotal <= 0 || subtotal >= freeThreshold ? 0m : flatFee;
}
