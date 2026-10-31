using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Web;
using Nexus.Reviews;
using Nexus.Reviews.Clients;
using Nexus.Reviews.Data;
using Nexus.Reviews.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Review Service");
builder.Services.AddServiceDbContext<ReviewDb>(builder.Configuration);
builder.Services.AddServiceClient<IOrdersClient, OrdersClient>(builder.Configuration, "Orders");

var app = builder.Build();
app.UseNexusDefaults();
app.MapReviewEndpoints();
await app.InitializeDatabaseAsync<ReviewDb>(async (db, _) => await ReviewSeeder.SeedAsync(db));
app.Run();

namespace Nexus.Reviews
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class ReviewsApiMarker { }
}
