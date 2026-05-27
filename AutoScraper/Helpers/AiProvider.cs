using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ScraperTemplate.Helpers;

public enum AiProvider
{
    HuggingFace,
    Anthropic
}

/// <summary>
/// Sends element summaries to an AI model and parses returned document selectors.
/// Supports both Hugging Face and Anthropic — switch via AiProvider in Program.cs.
/// </summary>
public class AiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly AiProvider _provider;
    private readonly string _model;

    public AiClient(string apiKey, AiProvider provider = AiProvider.HuggingFace,
        string? model = null)
    {
        _apiKey = apiKey;
        _provider = provider;
        _model = model ?? provider switch
        {
            AiProvider.HuggingFace => "mistralai/Mistral-7B-Instruct-v0.3",
            AiProvider.Anthropic   => "claude-haiku-4-5-20251001", // fastest + cheapest
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        if (provider == AiProvider.HuggingFace)
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// Asks the model to identify which elements match the scraping goal.
    /// </summary>
    public async Task<List<ElementSummary>> FindDocumentElementsAsync(
        List<ElementSummary> candidates,
        string goal = "regulatory documents, guidelines, or PDF files")
    {
        if (candidates.Count == 0) return [];

        var elementList = string.Join("\n", candidates.Select(c => c.ToString()));
        var prompt = $"""
            You are helping a web scraper identify document links on a webpage.
            
            Goal: Find all elements that are {goal}.
            
            Here are the elements found on the page:
            {elementList}
            
            Return ONLY a JSON array of index numbers for elements that match the goal.
            Example response: [1, 4, 7]
            If none match, return: []
            Return nothing else — no explanation, no markdown, just the JSON array.
            """;

        var response = await CallApiAsync(prompt);
        return ParseIndexResponse(response, candidates);
    }

    /// <summary>
    /// Asks the model to identify the next page button if pagination is present.
    /// </summary>
    public async Task<ElementSummary?> FindNextPageElementAsync(
        List<ElementSummary> candidates)
    {
        if (candidates.Count == 0) return null;

        var elementList = string.Join("\n", candidates.Select(c => c.ToString()));
        var prompt = $"""
            You are helping a web scraper navigate pagination.
            
            Here are the elements found on the page:
            {elementList}
            
            Return ONLY the index number of the "next page" or pagination button.
            Example response: 12
            If there is no next page button, return: -1
            Return nothing else — no explanation, no markdown, just the number.
            """;

        var response = await CallApiAsync(prompt);

        if (int.TryParse(response.Trim(), out var index) && index != -1)
            return candidates.FirstOrDefault(c => c.Index == index);

        return null;
    }

    // -------------------------------------------------------------------------
    // API calls
    // -------------------------------------------------------------------------

    private Task<string> CallApiAsync(string prompt) => _provider switch
    {
        AiProvider.HuggingFace => CallHuggingFaceAsync(prompt),
        AiProvider.Anthropic   => CallAnthropicAsync(prompt),
        _ => throw new ArgumentOutOfRangeException()
    };

    private async Task<string> CallHuggingFaceAsync(string prompt)
    {
        var formattedPrompt = $"<s>[INST] {prompt} [/INST]";
        var payload = JsonSerializer.Serialize(new
        {
            inputs = formattedPrompt,
            parameters = new
            {
                max_new_tokens = 100,
                temperature = 0.1,
                return_full_text = false
            }
        });

        var endpoints = new[]
        {
            $"https://api-inference.huggingface.co/models/{_model}",
            $"https://router.huggingface.co/models/{_model}"
        };

        foreach (var endpoint in endpoints)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(endpoint, content);

                    if ((int)response.StatusCode == 503)
                    {
                        Console.WriteLine("[AI] Model loading, waiting 20s...");
                        await Task.Delay(20000);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement[0]
                        .GetProperty("generated_text")
                        .GetString() ?? "";
                }
                catch (HttpRequestException ex)
                    when (ex.InnerException is System.Net.Sockets.SocketException)
                {
                    Console.WriteLine($"[AI] Network error on {endpoint} — trying next");
                    break;
                }
                catch (Exception ex) when (attempt < 3)
                {
                    Console.WriteLine($"[AI] Attempt {attempt} failed — {ex.Message}");
                    await Task.Delay(3000 * attempt);
                }
            }
        }

        throw new ScraperException("[AI] Hugging Face unreachable — switch to Anthropic in Program.cs");
    }

    private async Task<string> CallAnthropicAsync(string prompt)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model = _model,
            max_tokens = 100,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        });

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.anthropic.com/v1/messages");
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "";
            }
            catch (Exception ex) when (attempt < 3)
            {
                Console.WriteLine($"[AI] Attempt {attempt} failed — {ex.Message}");
                await Task.Delay(3000 * attempt);
            }
        }

        throw new ScraperException("[AI] Anthropic API failed after 3 attempts");
    }

    // -------------------------------------------------------------------------
    // Response parsing
    // -------------------------------------------------------------------------

    private static List<ElementSummary> ParseIndexResponse(
        string response, List<ElementSummary> candidates)
    {
        try
        {
            var cleaned = response.Trim();
            var start = cleaned.IndexOf('[');
            var end = cleaned.LastIndexOf(']');
            if (start == -1 || end == -1) return [];

            var indices = JsonSerializer.Deserialize<List<int>>(
                cleaned[start..(end + 1)]) ?? [];
            return candidates.Where(c => indices.Contains(c.Index)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AI] Failed to parse response: {ex.Message}");
            Console.WriteLine($"[AI] Raw response: {response}");
            return [];
        }
    }
}