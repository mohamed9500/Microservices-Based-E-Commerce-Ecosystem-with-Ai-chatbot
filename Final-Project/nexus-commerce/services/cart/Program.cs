using Microsoft.EntityFrameworkCore;
using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Messaging;
using Nexus.BuildingBlocks.Web;
using Nexus.Cart;
using Nexus.Cart.Clients;
using Nexus.Cart.Data;
using Nexus.Cart.Endpoints;
using Nexus.Contracts.Events;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Cart Service");
builder.Services.AddServiceDbContext<CartDb>(builder.Configuration);
builder.Services.AddServiceClient<ICatalogClient, CatalogClient>(builder.Configuration, "Catalog");
builder.Services.AddServiceClient<IInventoryClient, InventoryClient>(builder.Configuration, "Inventory");

// When an order is created the customer's cart is emptied (idempotent via the inbox).
builder.Services.AddMessaging<CartDb>(builder.Configuration, "cart.q", r => r.On<OrderCreated>(async (sp, e, ct) =>
{
    var db = sp.GetRequiredService<CartDb>();
    var rows = await db.Items.Where(i => i.UserId == e.UserId).ToListAsync(ct);
    db.Items.RemoveRange(rows);
}));

var app = builder.Build();
app.UseNexusDefaults();
app.MapCartEndpoints();
await app.InitializeDatabaseAsync<CartDb>();
app.Run();

namespace Nexus.Cart
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class CartApiMarker { }
}
