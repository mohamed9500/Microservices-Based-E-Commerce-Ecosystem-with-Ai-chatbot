using Nexus.Assistant.Tools;

namespace Nexus.Assistant.Llm;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "none";   // none | openai (any OpenAI-compatible API) | anthropic
    public string ApiKey { get; set; } = "";          // environment variable Ai__ApiKey only; never sent to the browser
    public string Model { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxTokens { get; set; } = 1024;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model) &&
        Provider.Trim().ToLowerInvariant() is "openai" or "anthropic";

    public string NormalizedProvider => Provider.Trim().ToLowerInvariant();
}

public enum LlmRole { User, Assistant, Tool }
public sealed record ToolCall(string Id, string Name, string ArgumentsJson);

public sealed class LlmMessage
{
    public LlmRole Role { get; init; }
    public string? Text { get; init; }
    public List<ToolCall>? ToolCalls { get; init; }
    public string? ToolCallId { get; init; }
}

public sealed record LlmTurn(string? Text, IReadOnlyList<ToolCall> ToolCalls);

public sealed class LlmException(string message) : Exception(message);

/// <summary>Provider-independent chat-with-tools abstraction. Implementations only translate wire formats.</summary>
public interface ILlmClient
{
    string ProviderName { get; }
    Task<LlmTurn> CompleteAsync(string system, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken ct);
}
