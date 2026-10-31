using System.ComponentModel.DataAnnotations;

namespace Nexus.Orders.Domain;

public sealed record ShippingAddress
{
    [Required, StringLength(150, MinimumLength = 2)] public string FullName { get; init; } = "";
    [Required, StringLength(200, MinimumLength = 3)] public string Line1 { get; init; } = "";
    [Required, StringLength(100)] public string City { get; init; } = "";
    [Required, StringLength(20)] public string PostalCode { get; init; } = "";
    [Required, StringLength(100)] public string Country { get; init; } = "";
    [Required, StringLength(30, MinimumLength = 5)] public string Phone { get; init; } = "";
}

public sealed record CreateOrderRequest
{
    [Required] public ShippingAddress? Shipping { get; init; }
    /// <summary>Only mock methods exist: mock_success | mock_decline. No card data is ever accepted.</summary>
    [Required, StringLength(30)] public string PaymentMethod { get; init; } = "mock_success";
}

public sealed record UpdateStatusRequest { [Required] public string Status { get; init; } = ""; }

public sealed record OrderItemDto(Guid ProductId, string Sku, string Name, string ImageUrl, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderDto(Guid Id, OrderStatus Status, decimal Subtotal, decimal ShippingFee, decimal Total, string Currency,
    string PaymentMethod, bool PaymentIsMock, string ShipName, string ShipLine1, string ShipCity, string ShipPostalCode,
    string ShipCountry, string ShipPhone, string? FailureReason, DateTime CreatedAtUtc, DateTime UpdatedAtUtc,
    IReadOnlyList<OrderItemDto> Items, Guid? UserId = null, string? UserEmail = null, string? CustomerName = null);

public sealed record DailySales(string Date, int Orders, decimal Revenue);
public sealed record TopProduct(Guid ProductId, string Name, int Quantity, decimal Revenue);
public sealed record OrderStats(int TotalOrders, int RevenueOrders, decimal Revenue, decimal AverageOrderValue,
    IReadOnlyDictionary<string, int> ByStatus, IReadOnlyList<DailySales> Daily, IReadOnlyList<TopProduct> TopProducts);

public static class OrderMapping
{
    public static OrderDto ToDto(this Order o, bool includeCustomer = false) => new(o.Id, o.Status, o.Subtotal, o.ShippingFee, o.Total,
        o.Currency, o.PaymentMethod, o.PaymentMethod.StartsWith("mock_"), o.ShipName, o.ShipLine1, o.ShipCity, o.ShipPostalCode,
        o.ShipCountry, o.ShipPhone, o.FailureReason, o.CreatedAtUtc, o.UpdatedAtUtc,
        o.Items.Select(i => new OrderItemDto(i.ProductId, i.Sku, i.Name, i.ImageUrl, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity)).ToList(),
        includeCustomer ? o.UserId : null, includeCustomer ? o.UserEmail : null, includeCustomer ? o.CustomerName : null);
}
