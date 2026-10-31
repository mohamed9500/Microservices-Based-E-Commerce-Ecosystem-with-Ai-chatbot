using System.ComponentModel.DataAnnotations;

namespace Nexus.Assistant.Domain;

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = [];
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string Role { get; set; } = "user";           // user | assistant
    public string Content { get; set; } = "";
    public string? ProductsJson { get; set; }
    public string? Mode { get; set; }                    // llm | fallback
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record HistoryItem
{
    [Required, RegularExpression("^(user|assistant)$")] public string Role { get; init; } = "user";
    [Required, StringLength(1000)] public string Content { get; init; } = "";
}

public sealed record ChatRequest
{
    public Guid? ConversationId { get; init; }
    [Required, StringLength(1000, MinimumLength = 1)] public string Message { get; init; } = "";
    [StringLength(10)] public string? Locale { get; init; }
    /// <summary>Only used for anonymous visitors (authenticated users have server-side history). Treated as untrusted.</summary>
    public List<HistoryItem>? History { get; init; }
}

public sealed record ProductCard(Guid Id, string Name, decimal Price, string Currency, string ImageUrl, string Category,
    string Brand, string Availability, double? Rating);

public sealed record ChatResponse(Guid? ConversationId, string Reply, string Mode, string? Provider, bool Degraded,
    string? Notice, IReadOnlyList<ProductCard> Products);

public sealed record ConversationSummary(Guid Id, string Title, DateTime UpdatedAtUtc);
public sealed record MessageDto(string Role, string Content, string? Mode, IReadOnlyList<ProductCard> Products, DateTime CreatedAtUtc);
