using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Nexus.BuildingBlocks.Web;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> Value = new();
    public static string? Current { get => Value.Value; set => Value.Value = value; }
}

public sealed class CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers[Header].FirstOrDefault();
        if (!IsValid(id)) id = Guid.NewGuid().ToString("N");
        ctx.Items[Header] = id;
        ctx.Response.Headers[Header] = id;
        CorrelationContext.Current = id;
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id! }))
        {
            await next(ctx);
        }
    }

    private static bool IsValid(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 64 && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}

/// <summary>Forwards the correlation id on outgoing service-to-service HTTP calls.</summary>
public sealed class CorrelationHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var id = CorrelationContext.Current;
        if (!string.IsNullOrEmpty(id) && !request.Headers.Contains(CorrelationMiddleware.Header))
            request.Headers.TryAddWithoutValidation(CorrelationMiddleware.Header, id);
        return base.SendAsync(request, ct);
    }
}
