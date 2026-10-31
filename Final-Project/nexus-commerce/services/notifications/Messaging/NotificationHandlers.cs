using System.Text;
using Nexus.BuildingBlocks.Messaging;
using Nexus.Contracts.Events;
using Nexus.Notifications.Data;
using Nexus.Notifications.Domain;
using Nexus.Notifications.Services;

namespace Nexus.Notifications.Messaging;

public sealed class NotificationHandlers(NotificationDb db, IEmailSender sender)
{
    public static string BuildConfirmationBody(OrderConfirmed e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Thank you for your order!").AppendLine();
        sb.AppendLine($"Order reference: {e.OrderId}");
        foreach (var i in e.Items) sb.AppendLine($" - {i.Quantity} x {i.Name} @ {i.UnitPrice:0.00} {e.Currency}");
        sb.AppendLine().AppendLine($"Total: {e.Total:0.00} {e.Currency}");
        return sb.ToString();
    }

    public async Task OnOrderConfirmedAsync(OrderConfirmed e, CancellationToken ct) =>
        await SendAsync(e.UserId, e.OrderId, "order-confirmed", e.UserEmail, "Your Nexus Commerce order is confirmed", BuildConfirmationBody(e), ct);

    public async Task OnOrderCancelledAsync(OrderCancelled e, CancellationToken ct) =>
        await SendAsync(e.UserId, e.OrderId, "order-cancelled", e.UserEmail, "Your Nexus Commerce order was cancelled",
            $"Order {e.OrderId} was cancelled.\nReason: {e.Reason}\nNo charge remains on this order.", ct);

    private async Task SendAsync(Guid userId, Guid orderId, string kind, string to, string subject, string body, CancellationToken ct)
    {
        var result = await sender.SendAsync(new EmailMessage(to, subject, body), ct);
        db.Notifications.Add(new NotificationRecord
        {
            UserId = userId, OrderId = orderId, Kind = kind, ToEmail = to, Subject = subject, Body = body,
            Status = result.Status, Provider = result.Provider, Error = result.Error
        });
    }
}
