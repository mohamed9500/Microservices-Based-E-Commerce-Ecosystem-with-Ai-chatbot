using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Messaging;
using Nexus.BuildingBlocks.Web;
using Nexus.Contracts.Events;
using Nexus.Inventory;
using Nexus.Inventory.Data;
using Nexus.Inventory.Endpoints;
using Nexus.Inventory.Messaging;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Inventory Service");
builder.Services.AddServiceDbContext<InventoryDb>(builder.Configuration);
builder.Services.AddScoped<ReservationService>();
builder.Services.AddMessaging<InventoryDb>(builder.Configuration, "inventory.q", r => r
    .On<OrderCreated>((sp, e, ct) => sp.GetRequiredService<ReservationService>().ReserveAsync(e, ct))
    .On<OrderConfirmed>((sp, e, ct) => sp.GetRequiredService<ReservationService>().CommitAsync(e.OrderId, ct))
    .On<OrderCancelled>((sp, e, ct) => sp.GetRequiredService<ReservationService>().ReleaseAsync(e.OrderId, ct)));

var app = builder.Build();
app.UseNexusDefaults();
app.MapInventoryEndpoints();
await app.InitializeDatabaseAsync<InventoryDb>(async (db, _) => await InventorySeeder.SeedAsync(db));
app.Run();

namespace Nexus.Inventory
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class InventoryApiMarker { }
}
