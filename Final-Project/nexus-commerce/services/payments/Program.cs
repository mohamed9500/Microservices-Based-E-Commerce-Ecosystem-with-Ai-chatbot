using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Messaging;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts.Events;
using Nexus.Payments;
using Nexus.Payments.Data;
using Nexus.Payments.Endpoints;
using Nexus.Payments.Messaging;
using Nexus.Payments.Providers;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Payment Service (MOCK provider)");
builder.Services.AddServiceDbContext<PaymentDb>(builder.Configuration);

var providerName = builder.Configuration["Payments:Provider"] ?? "mock";
if (!string.Equals(providerName, "mock", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException($"Payments:Provider '{providerName}' is not implemented. Only 'mock' is available; implement IPaymentProvider for a real gateway.");
builder.Services.AddSingleton<IPaymentProvider, MockPaymentProvider>();

builder.Services.AddScoped<PaymentHandlers>();
builder.Services.AddMessaging<PaymentDb>(builder.Configuration, "payments.q", r => r
    .On<InventoryReserved>((sp, e, ct) => sp.GetRequiredService<PaymentHandlers>().OnInventoryReservedAsync(e, ct))
    .On<OrderCancelled>((sp, e, ct) => sp.GetRequiredService<PaymentHandlers>().OnOrderCancelledAsync(e, ct)));

var app = builder.Build();
app.UseNexusDefaults();
app.MapPaymentEndpoints();
app.Logger.LogWarning("Payment provider = MOCK. No real charges are made.");
await app.InitializeDatabaseAsync<PaymentDb>();
app.Run();

namespace Nexus.Payments
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class PaymentsApiMarker { }
}
