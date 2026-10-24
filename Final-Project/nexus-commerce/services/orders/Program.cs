using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Messaging;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts.Events;
using Nexus.Orders;
using Nexus.Orders.Clients;
using Nexus.Orders.Data;
using Nexus.Orders.Endpoints;
using Nexus.Orders.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Order Service");
builder.Services.AddServiceDbContext<OrderDb>(builder.Configuration);
builder.Services.AddServiceClient<ICartClient, CartClient>(builder.Configuration, "Cart");
builder.Services.AddScoped<OrderSagaHandlers>();
builder.Services.AddMessaging<OrderDb>(builder.Configuration, "orders.q", r => r
    .On<InventoryReserved>((sp, e, ct) => sp.GetRequiredService<OrderSagaHandlers>().OnInventoryReservedAsync(e, ct))
    .On<InventoryReservationFailed>((sp, e, ct) => sp.GetRequiredService<OrderSagaHandlers>().OnInventoryFailedAsync(e, ct))
    .On<PaymentAuthorized>((sp, e, ct) => sp.GetRequiredService<OrderSagaHandlers>().OnPaymentAuthorizedAsync(e, ct))
    .On<PaymentFailed>((sp, e, ct) => sp.GetRequiredService<OrderSagaHandlers>().OnPaymentFailedAsync(e, ct)));

var app = builder.Build();
app.UseNexusDefaults();
app.MapOrderEndpoints();
await app.InitializeDatabaseAsync<OrderDb>();
app.Run();

namespace Nexus.Orders
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class OrdersApiMarker { }
}
