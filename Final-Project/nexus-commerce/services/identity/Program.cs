using Microsoft.AspNetCore.Identity;
using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Web;
using Nexus.Identity;
using Nexus.Identity.Data;
using Nexus.Identity.Domain;
using Nexus.Identity.Endpoints;
using Nexus.Identity.Services;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus Identity Service");
builder.Services.AddServiceDbContext<IdentityDb>(builder.Configuration);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();
app.UseNexusDefaults();
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapAdminUserEndpoints();

await app.InitializeDatabaseAsync<IdentityDb>(async (db, sp) =>
    await AdminSeeder.SeedAsync(db, sp.GetRequiredService<IPasswordHasher<User>>(), app.Configuration, app.Environment,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("AdminSeeder")));

app.Run();

namespace Nexus.Identity
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class IdentityApiMarker { }
}
