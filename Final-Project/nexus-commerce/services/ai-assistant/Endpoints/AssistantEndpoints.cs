using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexus.Assistant.Chat;
using Nexus.Assistant.Data;
using Nexus.Assistant.Domain;
using Nexus.Assistant.Llm;
using Nexus.BuildingBlocks.Auth;
using Nexus.BuildingBlocks.Web;

namespace Nexus.Assistant.Endpoints;

public static class AssistantEndpoints
{
    public static void MapAssistantEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/assistant").WithTags("Assistant");

        // Honest capability report: the UI shows whether an AI model or the basic fallback is active.
        g.MapGet("/status", (ChatOrchestrator orchestrator, IOptions<LlmOptions> opts) =>
            Results.Ok(new
            {
                mode = orchestrator.Mode,
                provider = orchestrator.LlmAvailable ? opts.Value.NormalizedProvider : null,
                message = orchestrator.LlmAvailable
                    ? "AI model with read-only catalog tools."
                    : "Basic rule-based assistant (no AI model configured). It searches and compares real catalog products."
            }));

        g.MapPost("/chat", async (ChatRequest? req, HttpContext http, AssistantDb db, ChatOrchestrator orchestrator, CancellationToken ct) =>
        {
            req.Validated();
            var message = req!.Message.Trim();
            if (message.Length == 0) throw new ValidationFailedException("message", "Message must not be empty.");
            if (req.History is { Count: > 10 }) throw new ValidationFailedException("history", "At most 10 history items are accepted.");
            foreach (var h in req.History ?? []) h.Validated();

            var userId = http.User.Identity?.IsAuthenticated == true ? http.User.TryGetUserId() : null;
            Conversation? conversation = null;
            IReadOnlyList<HistoryItem> history = req.History ?? [];

            if (userId != null)
            {
                if (req.ConversationId is { } cid)
                    conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == userId, ct)
                                   ?? throw new NotFoundException("Conversation not found.");
                conversation ??= new Conversation { UserId = userId.Value, Title = message.Length > 60 ? message[..60] + "..." : message };
                history = (await db.Messages.AsNoTracking().Where(m => m.ConversationId == conversation.Id)
                        .OrderByDescending(m => m.CreatedAtUtc).Take(10).ToListAsync(ct))
                    .OrderBy(m => m.CreatedAtUtc).Select(m => new HistoryItem { Role = m.Role, Content = m.Content }).ToList();
            }

            var result = await orchestrator.RespondAsync(message, history, ct);

            if (conversation != null)
            {
                if (db.Entry(conversation).State == EntityState.Detached) db.Conversations.Add(conversation);
                conversation.UpdatedAtUtc = DateTime.UtcNow;
                var now = DateTime.UtcNow;
                db.Messages.Add(new ChatMessage { Conversation = conversation, Role = "user", Content = message, CreatedAtUtc = now });
                db.Messages.Add(new ChatMessage
                {
                    Conversation = conversation, Role = "assistant", Content = result.Reply, Mode = result.Mode,
                    ProductsJson = JsonSerializer.Serialize(result.Products, JsonDefaults.Options), CreatedAtUtc = now.AddMilliseconds(1)
                });
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new ChatResponse(conversation?.Id, result.Reply, result.Mode, result.Provider, result.Degraded, result.Notice, result.Products));
        }).RequireRateLimiting("chat");

        var mine = g.MapGroup("/conversations").RequireAuthorization("authenticated");

        mine.MapGet("/", async (HttpContext http, AssistantDb db, CancellationToken ct) =>
        {
            var uid = http.User.GetUserId();
            return Results.Ok(await db.Conversations.AsNoTracking().Where(c => c.UserId == uid)
                .OrderByDescending(c => c.UpdatedAtUtc).Take(30)
                .Select(c => new ConversationSummary(c.Id, c.Title, c.UpdatedAtUtc)).ToListAsync(ct));
        });

        mine.MapGet("/{id:guid}", async (Guid id, HttpContext http, AssistantDb db, CancellationToken ct) =>
        {
            var uid = http.User.GetUserId();
            if (!await db.Conversations.AnyAsync(c => c.Id == id && c.UserId == uid, ct)) throw new NotFoundException("Conversation not found.");
            var msgs = await db.Messages.AsNoTracking().Where(m => m.ConversationId == id).OrderBy(m => m.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(msgs.Select(m => new MessageDto(m.Role, m.Content, m.Mode,
                m.ProductsJson == null ? [] : JsonSerializer.Deserialize<List<ProductCard>>(m.ProductsJson, JsonDefaults.Options) ?? [], m.CreatedAtUtc)));
        });

        mine.MapDelete("/{id:guid}", async (Guid id, HttpContext http, AssistantDb db, CancellationToken ct) =>
        {
            var uid = http.User.GetUserId();
            var c = await db.Conversations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct) ?? throw new NotFoundException("Conversation not found.");
            db.Conversations.Remove(c);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
