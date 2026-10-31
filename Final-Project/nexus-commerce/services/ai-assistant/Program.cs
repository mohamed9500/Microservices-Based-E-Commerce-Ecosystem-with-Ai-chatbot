using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.Assistant;
using Nexus.Assistant.Catalog;
using Nexus.Assistant.Chat;
using Nexus.Assistant.Data;
using Nexus.Assistant.Endpoints;
using Nexus.Assistant.Fallback;
using Nexus.Assistant.Llm;
using Nexus.Assistant.Tools;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Data;
using Nexus.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus AI Shopping Assistant");
builder.Services.AddServiceDbContext<AssistantDb>(builder.Configuration);

// Sibling services (read-only catalog facts).
foreach (var (name, key) in new[] { (HttpCatalogGateway.CatalogClient, "Catalog"), (HttpCatalogGateway.InventoryClient, "Inventory"), (HttpCatalogGateway.ReviewsClient, "Reviews") })
{
    var url = builder.Configuration[$"Services:{key}"] ?? throw new InvalidOperationException($"Services:{key} is not configured.");
    builder.Services.AddHttpClient(name, c => { c.BaseAddress = new Uri(url.TrimEnd('/') + "/"); c.Timeout = TimeSpan.FromSeconds(10); })
        .AddHttpMessageHandler<Nexus.BuildingBlocks.Web.CorrelationHandler>();
}
builder.Services.AddScoped<ICatalogGateway, HttpCatalogGateway>();
builder.Services.AddScoped<ToolRegistry>();
builder.Services.AddScoped<RuleBasedAssistant>();

// LLM provider: configured purely through environment variables (Ai__Provider, Ai__ApiKey, Ai__Model, Ai__BaseUrl).
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("Ai"));
var llm = builder.Configuration.GetSection("Ai").Get<LlmOptions>() ?? new LlmOptions();
if (llm.IsConfigured)
{
    if (llm.NormalizedProvider == "anthropic")
        builder.Services.AddHttpClient<ILlmClient, AnthropicClient>(c => c.Timeout = Timeout.InfiniteTimeSpan);
    else
        builder.Services.AddHttpClient<ILlmClient, OpenAiCompatibleClient>(c => c.Timeout = Timeout.InfiniteTimeSpan);
}
builder.Services.AddScoped<ChatOrchestrator>();

// Rate limiting per signed-in user (or client IP for anonymous visitors).
var limit = builder.Configuration.GetValue("Assistant:RateLimitPerMinute", 20);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, _) => await ProblemWriter.WriteAsync(ctx.HttpContext, 429, "Too Many Requests",
        "You are sending messages too quickly. Please wait a moment.");
    o.AddPolicy("chat", ctx =>
    {
        var key = ctx.User.TryGetUserId()?.ToString()
                  ?? ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                  ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    });
});

var app = builder.Build();
app.UseNexusDefaults();
app.UseRateLimiter();
app.MapAssistantEndpoints();
app.Logger.LogInformation("Assistant mode: {Mode}", llm.IsConfigured ? $"LLM ({llm.NormalizedProvider})" : "rule-based fallback (no AI key configured)");
await app.InitializeDatabaseAsync<AssistantDb>();
app.Run();

namespace Nexus.Assistant
{
    /// <summary>Entry-point marker used by WebApplicationFactory in integration tests.</summary>
    public sealed class AssistantApiMarker { }
}
