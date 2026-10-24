namespace Nexus.Contracts.Events;

[EventContract("payment.authorized", 1)]
public sealed record PaymentAuthorized(Guid OrderId, Guid PaymentId, decimal Amount, string Currency, string Provider);

[EventContract("payment.failed", 1)]
public sealed record PaymentFailed(Guid OrderId, string Reason);
