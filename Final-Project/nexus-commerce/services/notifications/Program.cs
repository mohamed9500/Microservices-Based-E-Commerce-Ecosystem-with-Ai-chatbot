using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Messaging;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts.Events;
using Nexus.Notifications;
using Nexus.Notifications.Data;
using Nexus.Notifications.Endpoints;
using Nexus.Notifications.Messaging;
using Nexus.Notifications.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Notification Service");
builder.Services.AddServiceDbContext<NotificationDb>(builder.Configuration);

var provider = builder.Configuration["Email:Provider"] ?? "log";
if (provider.Equals("smtp", StringComparison.OrdinalIgnoreCase)) builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();

builder.Services.AddScoped<NotificationHandlers>();
builder.Services.AddMessaging<NotificationDb>(builder.Configuration, "notifications.q", r => r
    .On<OrderConfirmed>((sp, e, ct) => sp.GetRequiredService<NotificationHandlers>().OnOrderConfirmedAsync(e, ct))
    .On<OrderCancelled>((sp, e, ct) => sp.GetRequiredService<NotificationHandlers>().OnOrderCancelledAsync(e, ct)));

var app = builder.Build();
app.UseNexusDefaults();
app.MapNotificationEndpoints();
await app.InitializeDatabaseAsync<NotificationDb>();
app.Run();

namespace Nexus.Notifications
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class NotificationsApiMarker { }
}
