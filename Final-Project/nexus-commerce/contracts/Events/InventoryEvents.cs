namespace Nexus.Contracts.Events;

/// <summary>
/// Stock was reserved for the order. Total/Currency/PaymentMethod are passed through
/// so the Payment service does not need to query Orders (it still never trusts clients).
/// </summary>
[EventContract("inventory.reserved", 1)]
public sealed record InventoryReserved(
    Guid OrderId, Guid UserId, decimal Total, string Currency, string PaymentMethod);

[EventContract("inventory.reservation-failed", 1)]
public sealed record InventoryReservationFailed(Guid OrderId, string Reason);
