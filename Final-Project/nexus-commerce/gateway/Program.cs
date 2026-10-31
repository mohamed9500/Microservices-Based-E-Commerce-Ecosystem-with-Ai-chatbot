using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Nexus.BuildingBlocks.Web;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);
builder.AddNexusDefaults("Nexus API Gateway");

var globalLimit = builder.Configuration.GetValue("RateLimit:GlobalPerMinute", 600);
var authLimit = builder.Configuration.GetValue("RateLimit:AuthPerMinute", 15);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, _) => await ProblemWriter.WriteAsync(ctx.HttpContext, 429, "Too Many Requests", "Rate limit exceeded. Please slow down.");
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = globalLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // Stricter limit on login/registration to slow credential stuffing.
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter("auth:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddHttpClient("monitor", c => c.Timeout = TimeSpan.FromSeconds(4));
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(t => t.AddRequestTransform(ctx =>
    {
        // Propagate the correlation id to downstream services.
        ctx.ProxyRequest.Headers.Remove(CorrelationMiddleware.Header);
        if (ctx.HttpContext.Items[CorrelationMiddleware.Header] is string id)
            ctx.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationMiddleware.Header, id);
        return ValueTask.CompletedTask;
    }));

var app = builder.Build();
app.UseNexusDefaults();
app.UseRateLimiter();

// Admin-only operational view: health of every downstream service.
app.MapGet("/api/admin/monitoring/services", async (IHttpClientFactory factory, IConfiguration config, CancellationToken ct) =>
{
    var services = config.GetSection("Monitoring:Services").GetChildren().ToDictionary(c => c.Key, c => c.Value ?? "");
    var tasks = services.Select(async kv =>
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var res = await factory.CreateClient("monitor").GetAsync($"{kv.Value.TrimEnd('/')}/health", ct);
            return new { name = kv.Key, healthy = res.IsSuccessStatusCode, status = (int)res.StatusCode, latencyMs = sw.ElapsedMilliseconds, error = (string?)null };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new { name = kv.Key, healthy = false, status = 0, latencyMs = sw.ElapsedMilliseconds, error = (string?)ex.Message };
        }
    });
    return Results.Ok(new { checkedAtUtc = DateTime.UtcNow, services = (await Task.WhenAll(tasks)).OrderBy(s => s.name) });
}).RequireAuthorization("admin").WithTags("Monitoring");

app.MapGet("/", () => Results.Ok(new { service = "Nexus API Gateway", health = "/health" })).AllowAnonymous();
app.MapReverseProxy();
app.Run();

public partial class Program { }
