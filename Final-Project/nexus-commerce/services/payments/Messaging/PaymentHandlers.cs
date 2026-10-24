using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Contracts.Events;
using Nexus.Payments.Data;
using Nexus.Payments.Domain;
using Nexus.Payments.Providers;

namespace Nexus.Payments.Messaging;

public sealed class PaymentHandlers(PaymentDb db, IPaymentProvider provider)
{
    public async Task OnInventoryReservedAsync(InventoryReserved e, CancellationToken ct)
    {
        if (await db.Payments.AnyAsync(p => p.OrderId == e.OrderId, ct)) return; // already handled
        var payment = new Payment
        {
            OrderId = e.OrderId, UserId = e.UserId, Amount = e.Total, Currency = e.Currency,
            Provider = provider.Name, IsMock = provider.IsMock
        };
        var result = await provider.AuthorizeAsync(new PaymentRequest(e.OrderId, e.Total, e.Currency, e.PaymentMethod), ct);
        payment.UpdatedAtUtc = DateTime.UtcNow;
        db.Payments.Add(payment);
        if (result.Succeeded)
        {
            payment.Status = PaymentStatus.Authorized;
            payment.ProviderReference = result.Reference;
            db.EnqueueEvent(new PaymentAuthorized(e.OrderId, payment.Id, e.Total, e.Currency, provider.Name));
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = result.FailureReason;
            db.EnqueueEvent(new PaymentFailed(e.OrderId, result.FailureReason ?? "Payment declined"));
        }
    }

    /// <summary>Order cancelled after authorisation: void the authorisation (no capture ever happened).</summary>
    public async Task OnOrderCancelledAsync(OrderCancelled e, CancellationToken ct)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == e.OrderId, ct);
        if (payment is not { Status: PaymentStatus.Authorized }) return;
        if (payment.ProviderReference != null) await provider.VoidAsync(payment.ProviderReference, ct);
        payment.Status = PaymentStatus.Voided;
        payment.UpdatedAtUtc = DateTime.UtcNow;
    }
}
