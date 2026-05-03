using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ExtraIsland.Shared;

public enum AiFilterApiType {
    [Description("Chat Completions API (兼容性更广)")]
    Chat,
    [Description("Responses API (支持联网搜索)")]
    Responses
}

public class AiRhesisFilter {
    readonly HttpClient _httpClient = new();

    public async Task<bool> CheckQuoteAsync(
        string endpoint,
        string apiKey,
        string model,
        string instructions,
        AiFilterApiType apiType,
        string quoteContent,
        string quoteAuthor,
        string quoteTitle,
        bool enableWebSearch,
        bool deepThinking,
        CancellationToken cancellationToken = default)
    {
        try {
            if (string.IsNullOrWhiteSpace(apiKey)) return true;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            string fullEndpoint = BuildEndpoint(endpoint, apiType);

            string suffix = deepThinking
                ? "\n\n句子：{0}\n作者：{1}\n出处：{2}\n\n请逐步分析，并在最后一行单独回复 是 或 否。"
                : "\n\n句子：{0}\n作者：{1}\n出处：{2}\n\n仅回复 是 或 否。";
            string prompt = instructions + string.Format(suffix, quoteContent, quoteAuthor, quoteTitle);
            DiagLog($"SENDING endpoint=[{fullEndpoint}] model=[{model}] prompt=[{prompt[..Math.Min(prompt.Length, 200)]}]");

            string? responseText = apiType switch {
                AiFilterApiType.Chat => await CallChatApiAsync(fullEndpoint, model, prompt, deepThinking, cancellationToken),
                AiFilterApiType.Responses => await CallResponsesApiAsync(fullEndpoint, model, prompt, enableWebSearch, deepThinking, cancellationToken),
                _ => null
            };

            if (responseText == null) return true;

            string verdict = deepThinking
                ? responseText.Trim().Split('\n')
                    .LastOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? responseText
                : responseText.Trim();

            bool approved = IsApproved(verdict);
            DiagLog($"response=[{responseText.Trim()}] verdict=[{verdict}] approved={approved}");
            return approved;
        }
        catch (Exception ex) {
            DiagLog($"EXCEPTION: {ex.Message}");
            GlobalConstants.HostInterfaces.PluginLogger?.LogWarning(ex, "AI 筛选请求失败，已放行该句子");
            return true;
        }
    }

    static string BuildEndpoint(string baseEndpoint, AiFilterApiType apiType) {
        string ep = baseEndpoint.TrimEnd('/');
        if (ep.EndsWith("/chat/completions") || ep.EndsWith("/responses")) return ep;
        return apiType switch {
            AiFilterApiType.Chat => $"{ep}/chat/completions",
            AiFilterApiType.Responses => $"{ep}/responses",
            _ => ep
        };
    }

    async Task<string?> CallChatApiAsync(string endpoint, string model, string prompt, bool deepThinking, CancellationToken ct) {
        var body = new {
            model,
            messages = new[] {
                new { role = "system", content = prompt },
                new { role = "user", content = "请判断。" }
            },
            max_tokens = deepThinking ? 2000 : 200,
            temperature = 0.0,
            reasoning_effort = deepThinking ? "high" : null
        };

        string json = JsonSerializer.Serialize(body, new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var response = await _httpClient.PostAsync(endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String)
        {
            string? result = content.GetString();
            DiagLog($"ChatAPI OK raw=[{responseJson[..Math.Min(responseJson.Length, 500)]}]");
            return result;
        }

        DiagLog($"ChatAPI UNEXPECTED FORMAT: raw=[{responseJson[..Math.Min(responseJson.Length, 500)]}]");
        return null;
    }

    async Task<string?> CallResponsesApiAsync(string endpoint, string model, string prompt, bool enableWebSearch, bool deepThinking, CancellationToken ct) {
        var toolsList = new List<object>();
        if (enableWebSearch) {
            toolsList.Add(new { type = "web_search" });
        }

        var body = new {
            model,
            input = prompt,
            tools = toolsList.Count > 0 ? toolsList.ToArray() : null,
            max_output_tokens = deepThinking ? 2000 : 200,
            temperature = 0.0,
            reasoning_effort = deepThinking ? "high" : null
        };

        string json = JsonSerializer.Serialize(body, new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var response = await _httpClient.PostAsync(endpoint,
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("output", out var output) &&
            output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray()) {
                if (item.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in content.EnumerateArray()) {
                        if (part.TryGetProperty("text", out var text) &&
                            text.ValueKind == JsonValueKind.String)
                        {
                            string? t = text.GetString();
                            if (!string.IsNullOrWhiteSpace(t)) return t;
                        }
                    }
                }
            }
        }

        DiagLog($"RespAPI UNEXPECTED FORMAT: raw=[{responseJson[..Math.Min(responseJson.Length, 300)]}]");
        return null;
    }

    static bool IsApproved(string text) {
        string t = text.Trim();
        if (t.StartsWith("否", StringComparison.Ordinal) ||
            t.StartsWith("不", StringComparison.Ordinal))
            return false;
        return t.StartsWith("是", StringComparison.Ordinal) ||
               t.Equals("YES", StringComparison.OrdinalIgnoreCase);
    }

    static void DiagLog(string message) {
        try {
            string logDir = GlobalConstants.PluginConfigFolder ?? Path.GetTempPath();
            string logFile = Path.Combine(logDir, "AiFilter.log");
            File.AppendAllText(logFile, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        } catch { }
    }
}
