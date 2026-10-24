using System.ComponentModel.DataAnnotations;

namespace Nexus.Inventory.Domain;

public sealed class StockItem
{
    public Guid ProductId { get; set; }
    public int OnHand { get; set; }
    public int Reserved { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Optimistic concurrency token: concurrent reservations of the same product cannot both succeed blindly.</summary>
    [Timestamp] public byte[]? RowVersion { get; set; }
    public int Available => OnHand - Reserved;
}

public enum ReservationStatus { Reserved, Committed, Released }

public sealed class StockReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Append-only history of every change to OnHand.</summary>
public sealed class StockAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public int Delta { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = "";
    public string Source { get; set; } = "admin";   // admin | order | order-cancel | seed
    public Guid? ActorUserId { get; set; }
    public Guid? OrderId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record StockDto(Guid ProductId, int Available, string Status);
public sealed record AdminStockDto(Guid ProductId, int OnHand, int Reserved, int Available, DateTime UpdatedAtUtc);

public sealed record AdjustRequest
{
    [Range(-100000, 100000)] public int Delta { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Reason { get; init; } = "";
}

public sealed record SetStockRequest
{
    [Range(0, 1000000)] public int OnHand { get; init; }
    [Required, StringLength(200, MinimumLength = 3)] public string Reason { get; init; } = "";
}
