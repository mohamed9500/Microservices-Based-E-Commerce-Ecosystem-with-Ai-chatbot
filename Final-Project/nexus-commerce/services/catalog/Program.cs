using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Web;
using Nexus.Catalog;
using Nexus.Catalog.Data;
using Nexus.Catalog.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Catalog Service");
builder.Services.AddServiceDbContext<CatalogDb>(builder.Configuration);
builder.Services.AddNexusCache(builder.Configuration);

var app = builder.Build();
app.UseNexusDefaults();
app.MapCatalogEndpoints();
await app.InitializeDatabaseAsync<CatalogDb>(async (db, _) => await CatalogSeeder.SeedAsync(db));
app.Run();

namespace Nexus.Catalog
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class CatalogApiMarker { }
}
