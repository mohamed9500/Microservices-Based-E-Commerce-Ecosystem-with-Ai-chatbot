using System.ComponentModel.DataAnnotations;

namespace Nexus.Cart.Domain;

public sealed class CartItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    /// <summary>Server-validated unit price, refreshed from the Catalog on every cart read.</summary>
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record CartItemDto(Guid ProductId, string Sku, string Name, string ImageUrl, decimal UnitPrice, int Quantity, decimal LineTotal);
public sealed record CartDto(IReadOnlyList<CartItemDto> Items, int ItemCount, decimal Subtotal, string Currency, IReadOnlyList<string> Warnings);

public sealed record AddItemRequest
{
    public Guid ProductId { get; init; }
    [Range(1, 20)] public int Quantity { get; init; } = 1;
}

public sealed record UpdateItemRequest
{
    [Range(1, 20)] public int Quantity { get; init; }
}
