namespace Nexus.Payments.Domain;

public enum PaymentStatus { Pending, Authorized, Failed, Voided }

/// <summary>A payment intent for one order. Contains no card data of any kind.</summary>
public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Provider { get; set; } = "";
    public bool IsMock { get; set; }
    public string? ProviderReference { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record PaymentDto(Guid Id, Guid OrderId, decimal Amount, string Currency, string Provider, bool IsMock,
    string Status, string? FailureReason, DateTime CreatedAtUtc, string Notice);

public static class PaymentMapping
{
    public static PaymentDto ToDto(this Payment p) => new(p.Id, p.OrderId, p.Amount, p.Currency, p.Provider, p.IsMock,
        p.Status.ToString(), p.FailureReason, p.CreatedAtUtc,
        p.IsMock ? "MOCK PAYMENT - simulated for development. No real money was charged." : "");
}
