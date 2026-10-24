using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Nexus.BuildingBlocks.Web;

public static class ProblemWriter
{
    public static Task WriteAsync(HttpContext ctx, int status, string title, string? detail = null,
        IDictionary<string, string[]>? errors = null)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        ctx.Items.TryGetValue(CorrelationMiddleware.Header, out var cid);
        var body = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{status}",
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["traceId"] = ctx.TraceIdentifier,
            ["correlationId"] = cid as string
        };
        if (errors is { Count: > 0 }) body["errors"] = errors;
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonDefaults.Options));
    }
}
