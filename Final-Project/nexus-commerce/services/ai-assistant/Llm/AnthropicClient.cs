using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Nexus.Assistant.Tools;

namespace Nexus.Assistant.Llm;

/// <summary>Anthropic Messages API wire format. BaseUrl defaults to https://api.anthropic.com.</summary>
public sealed class AnthropicClient(HttpClient http, IOptions<LlmOptions> options) : ILlmClient
{
    public string ProviderName => "anthropic";

    public async Task<LlmTurn> CompleteAsync(string system, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        var o = options.Value;
        var msgs = new JsonArray();
        JsonArray? pendingResults = null; // consecutive tool results must share one user message
        foreach (var m in messages)
        {
            if (m.Role != LlmRole.Tool) pendingResults = null;
            switch (m.Role)
            {
                case LlmRole.User:
                    msgs.Add(new JsonObject { ["role"] = "user", ["content"] = m.Text });
                    break;
                case LlmRole.Assistant:
                    var blocks = new JsonArray();
                    if (!string.IsNullOrWhiteSpace(m.Text)) blocks.Add(new JsonObject { ["type"] = "text", ["text"] = m.Text });
                    foreach (var tc in m.ToolCalls ?? [])
                        blocks.Add(new JsonObject { ["type"] = "tool_use", ["id"] = tc.Id, ["name"] = tc.Name, ["input"] = ParseInput(tc.ArgumentsJson) });
                    msgs.Add(new JsonObject { ["role"] = "assistant", ["content"] = blocks });
                    break;
                case LlmRole.Tool:
                    if (pendingResults == null)
                    {
                        pendingResults = new JsonArray();
                        msgs.Add(new JsonObject { ["role"] = "user", ["content"] = pendingResults });
                    }
                    pendingResults.Add(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = m.ToolCallId, ["content"] = m.Text });
                    break;
            }
        }

        var body = new JsonObject { ["model"] = o.Model, ["max_tokens"] = o.MaxTokens, ["system"] = system, ["messages"] = msgs };
        if (tools.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var t in tools)
                arr.Add(new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["input_schema"] = t.Schema.DeepClone() });
            body["tools"] = arr;
        }

        var baseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? "https://api.anthropic.com" : o.BaseUrl.TrimEnd('/');
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", o.ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new LlmException($"LLM provider returned HTTP {(int)res.StatusCode}.");

        var content = JsonNode.Parse(text)?["content"] as JsonArray ?? throw new LlmException("LLM provider returned no content.");
        var sb = new StringBuilder();
        var calls = new List<ToolCall>();
        foreach (var block in content)
        {
            switch (block?["type"]?.GetValue<string>())
            {
                case "text": sb.Append(block["text"]?.GetValue<string>()); break;
                case "tool_use":
                    calls.Add(new ToolCall(block["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                        block["name"]?.GetValue<string>() ?? "", block["input"]?.ToJsonString() ?? "{}"));
                    break;
            }
        }
        return new LlmTurn(sb.Length == 0 ? null : sb.ToString(), calls);
    }

    private static JsonNode ParseInput(string json)
    {
        try { return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new JsonObject(); }
        catch (System.Text.Json.JsonException) { return new JsonObject(); }
    }
}
