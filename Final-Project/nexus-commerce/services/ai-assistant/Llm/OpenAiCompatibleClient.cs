using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Nexus.Assistant.Tools;

namespace Nexus.Assistant.Llm;

/// <summary>Chat Completions wire format (OpenAI and compatible gateways). BaseUrl defaults to https://api.openai.com/v1.</summary>
public sealed class OpenAiCompatibleClient(HttpClient http, IOptions<LlmOptions> options) : ILlmClient
{
    public string ProviderName => "openai";

    public async Task<LlmTurn> CompleteAsync(string system, IReadOnlyList<LlmMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken ct)
    {
        var o = options.Value;
        var msgs = new JsonArray { new JsonObject { ["role"] = "system", ["content"] = system } };
        foreach (var m in messages)
        {
            switch (m.Role)
            {
                case LlmRole.User:
                    msgs.Add(new JsonObject { ["role"] = "user", ["content"] = m.Text });
                    break;
                case LlmRole.Assistant:
                    var a = new JsonObject { ["role"] = "assistant", ["content"] = m.Text };
                    if (m.ToolCalls is { Count: > 0 })
                    {
                        var calls = new JsonArray();
                        foreach (var tc in m.ToolCalls)
                            calls.Add(new JsonObject { ["id"] = tc.Id, ["type"] = "function", ["function"] = new JsonObject { ["name"] = tc.Name, ["arguments"] = tc.ArgumentsJson } });
                        a["tool_calls"] = calls;
                    }
                    msgs.Add(a);
                    break;
                case LlmRole.Tool:
                    msgs.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = m.ToolCallId, ["content"] = m.Text });
                    break;
            }
        }

        var body = new JsonObject { ["model"] = o.Model, ["messages"] = msgs };
        if (tools.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var t in tools)
                arr.Add(new JsonObject { ["type"] = "function", ["function"] = new JsonObject { ["name"] = t.Name, ["description"] = t.Description, ["parameters"] = t.Schema.DeepClone() } });
            body["tools"] = arr;
            body["tool_choice"] = "auto";
        }

        var baseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? "https://api.openai.com/v1" : o.BaseUrl.TrimEnd('/');
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);

        using var res = await http.SendAsync(req, ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new LlmException($"LLM provider returned HTTP {(int)res.StatusCode}.");

        var message = JsonNode.Parse(text)?["choices"]?[0]?["message"] ?? throw new LlmException("LLM provider returned no message.");
        var calls2 = new List<ToolCall>();
        if (message["tool_calls"] is JsonArray tcs)
            foreach (var tc in tcs)
                calls2.Add(new ToolCall(tc?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                    tc?["function"]?["name"]?.GetValue<string>() ?? "", tc?["function"]?["arguments"]?.GetValue<string>() ?? "{}"));
        return new LlmTurn(message["content"]?.GetValue<string>(), calls2);
    }
}
