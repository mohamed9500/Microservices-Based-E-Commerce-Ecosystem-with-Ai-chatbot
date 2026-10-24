using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Nexus.BuildingBlocks.Web;

public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            ctx.Response.StatusCode = 499; // client closed request
        }
        catch (Exception ex)
        {
            if (ctx.Response.HasStarted) throw;
            switch (ex)
            {
                case ValidationFailedException v:
                    await ProblemWriter.WriteAsync(ctx, v.Status, v.Title, v.Detail, v.Errors);
                    break;
                case AppException a:
                    if (a.Status >= 500) logger.LogError(ex, "Application error");
                    else logger.LogInformation("Request rejected: {Status} {Detail}", a.Status, a.Detail);
                    await ProblemWriter.WriteAsync(ctx, a.Status, a.Title, a.Detail);
                    break;
                case BadHttpRequestException b:
                    logger.LogInformation("Bad request: {Message}", b.Message);
                    await ProblemWriter.WriteAsync(ctx, 400, "Bad Request", "The request body or parameters are invalid.");
                    break;
                case DbUpdateConcurrencyException:
                    logger.LogWarning(ex, "Concurrency conflict");
                    await ProblemWriter.WriteAsync(ctx, 409, "Conflict", "The resource was modified by another request. Please retry.");
                    break;
                default:
                    logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
                    await ProblemWriter.WriteAsync(ctx, 500, "Internal Server Error",
                        "An unexpected error occurred. Quote the correlationId when reporting this.");
                    break;
            }
        }
    }
}
