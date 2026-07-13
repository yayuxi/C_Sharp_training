using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ScraperTemplate.Helpers;

public enum AiProvider
{
    HuggingFace,
    Anthropic,
    Ollama,
    Groq
}

/// <summary>
/// Sends HTML samples to an AI model and returns structured extraction plans
/// (container selector + field selectors) for use by AutoScraper.
/// Also handles pagination detection via element summary lists.
/// Switch provider and API key in Program.cs.
/// </summary>
public class AiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly AiProvider _provider;
    private readonly string _model;

    public string ProviderName => _provider switch
    {
        AiProvider.HuggingFace => "Hugging Face",
        AiProvider.Anthropic   => "Anthropic",
        AiProvider.Ollama      => "Ollama (local)",
        AiProvider.Groq        => "Groq",
        _ => "Unknown"
    };

    public AiClient(string apiKey, AiProvider provider = AiProvider.Ollama, string? model = null)
    {
        _apiKey   = apiKey;
        _provider = provider;
        _model    = model ?? provider switch
        {
            AiProvider.HuggingFace => "mistralai/Mistral-7B-Instruct-v0.3",
            AiProvider.Anthropic   => "claude-haiku-4-5-20251001",
            AiProvider.Ollama      => "mistral:7b-instruct-q4_0",
            AiProvider.Groq        => "llama-3.3-70b-versatile",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        _httpClient = new HttpClient
        {
            Timeout = provider == AiProvider.Ollama
                ? TimeSpan.FromSeconds(300)
                : TimeSpan.FromSeconds(60)
        };

        if (provider == AiProvider.HuggingFace)
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
    }

    // -------------------------------------------------------------------------
    // Page structure analysis
    // -------------------------------------------------------------------------

    /// <summary>
    /// Analyses a representative HTML sample and returns a plan describing
    /// which CSS selectors to use for the container and each field.
    /// </summary>
    public async Task<ExtractionPlan?> AnalysePageStructureAsync(
        string pageHtml, string goal, string sampleHint = "app-accordion-guidline")
    {
        var sample = ExtractRepresentativeSample(pageHtml, sampleHint);
        Console.WriteLine($"[AI] Sending {sample.Length} chars to model for analysis");
        Console.WriteLine($"[AI] HTML sample:\n{sample}\n[AI] End of sample");

        var isGuidelineGoal = sampleHint.Contains("guidline", StringComparison.OrdinalIgnoreCase);
        var jsonExample = """{"containerSelector":"CSS selector","fields":{"FieldName":"CSS selector"}}""";

        var fieldGuidance = isGuidelineGoal
            ? """
              Use EXACTLY these field names (only include ones you can find real selectors for):
              - "Summary" for the main descriptive paragraph text
              - "Date" for a date value (look for text that looks like a date, e.g. "27 October 1994")
              - "Step" for step information — look for text inside an <em> tag, e.g. "Step 5"
              """
            : """
              Use EXACTLY these field names (only include ones you can find real selectors for):
              - "Title" for the document name or link text
              - "Type" for any document category or type label, if present
              """;

        var prompt = $"""
            You are a web scraping expert. Analyse this HTML and return CSS selectors.
            
            Goal: Extract {goal}
            
            HTML sample:
            {sample}
            
            IMPORTANT: Only use class names and element names that actually appear in the HTML above.
            Do not invent class names. Do not return empty string selectors — omit fields you cannot find.
            
            {fieldGuidance}
            
            Return ONLY this JSON structure with real selectors from the HTML:
            {jsonExample}
            
            JSON:
            """;

        var response = await CallApiAsync(prompt, maxTokens: 300);
        return ParseExtractionPlan(response);
    }

    // -------------------------------------------------------------------------
    // Pagination detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Identifies the next page button from a list of candidate elements.
    /// Used as a fallback when CSS pagination selectors find nothing.
    /// </summary>
    public async Task<ElementSummary?> FindNextPageElementAsync(
        List<ElementSummary> candidates)
    {
        if (candidates.Count == 0) return null;

        var elementList = string.Join("\n", candidates.Select(c => c.ToString()));
        var prompt = $"""
            You are helping a web scraper find pagination controls.
            
            Here are the elements found on the page:
            {elementList}
            
            Find the "next page" pagination button or link. It should:
            - Have text like "Next", "→", "»", or a page number
            - Be a navigation control, NOT a content link
            - Stay on the same website (not link to external sites)
            - NOT be an author name, book title, tag, or any content element
            
            Return ONLY the index number of the next page control.
            Example response: 12
            If there is no next page button, return: -1
            Return nothing else — no explanation, no markdown, just the number.
            """;

        var response = await CallApiAsync(prompt, maxTokens: 10);
        if (int.TryParse(response.Trim(), out var index) && index != -1)
            return candidates.FirstOrDefault(c => c.Index == index);

        return null;
    }

    // -------------------------------------------------------------------------
    // API calls
    // -------------------------------------------------------------------------

    private Task<string> CallApiAsync(string prompt, int maxTokens = 100) => _provider switch
    {
        AiProvider.HuggingFace => CallHuggingFaceAsync(prompt, maxTokens),
        AiProvider.Anthropic   => CallAnthropicAsync(prompt, maxTokens),
        AiProvider.Ollama      => CallOllamaAsync(prompt, maxTokens),
        AiProvider.Groq        => CallGroqAsync(prompt, maxTokens),
        _ => throw new ArgumentOutOfRangeException()
    };

    private async Task<string> CallHuggingFaceAsync(string prompt, int maxTokens)
    {
        var payload = JsonSerializer.Serialize(new
        {
            inputs = $"<s>[INST] {prompt} [/INST]",
            parameters = new { max_new_tokens = maxTokens, temperature = 0.1, return_full_text = false }
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
                    var response = await _httpClient.PostAsync(endpoint,
                        new StringContent(payload, Encoding.UTF8, "application/json"));

                    if ((int)response.StatusCode == 503)
                    {
                        Console.WriteLine("[AI] Model loading, waiting 20s...");
                        await Task.Delay(20000);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement[0].GetProperty("generated_text").GetString() ?? "";
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

        throw new ScraperException("[AI] Hugging Face unreachable");
    }

    private async Task<string> CallAnthropicAsync(string prompt, int maxTokens)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model    = _model,
            max_tokens = maxTokens,
            messages = new[] { new { role = "user", content = prompt } }
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
                return doc.RootElement.GetProperty("content")[0]
                    .GetProperty("text").GetString() ?? "";
            }
            catch (Exception ex) when (attempt < 3)
            {
                Console.WriteLine($"[AI] Attempt {attempt} failed — {ex.Message}");
                await Task.Delay(3000 * attempt);
            }
        }

        throw new ScraperException("[AI] Anthropic API failed after 3 attempts");
    }

    private async Task<string> CallOllamaAsync(string prompt, int maxTokens)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model    = _model,
            messages = new[] { new { role = "user", content = prompt } },
            stream   = false,
            options  = new { temperature = 0.1, num_predict = maxTokens }
        });

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await _httpClient.PostAsync(
                    "http://localhost:11434/api/chat",
                    new StringContent(payload, Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                    Console.WriteLine($"[AI] Ollama error: {await response.Content.ReadAsStringAsync()}");

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("message")
                    .GetProperty("content").GetString() ?? "";
            }
            catch (Exception ex) when (attempt < 3)
            {
                Console.WriteLine($"[AI] Ollama attempt {attempt} failed — {ex.Message}");
                await Task.Delay(3000 * attempt);
            }
        }

        throw new ScraperException("[AI] Ollama failed — is it running? Try: ollama serve");
    }

    private async Task<string> CallGroqAsync(string prompt, int maxTokens)
    {
        var payload = JsonSerializer.Serialize(new
        {
            model      = _model,
            messages   = new[] { new { role = "user", content = prompt } },
            max_tokens = maxTokens,
            temperature = 0.1
        });

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.groq.com/openai/v1/chat/completions");
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiKey);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("choices")[0]
                    .GetProperty("message").GetProperty("content").GetString() ?? "";
            }
            catch (Exception ex) when (attempt < 3)
            {
                Console.WriteLine($"[AI] Groq attempt {attempt} failed — {ex.Message}");
                await Task.Delay(3000 * attempt);
            }
        }

        throw new ScraperException("[AI] Groq API failed after 3 attempts");
    }

    // -------------------------------------------------------------------------
    // HTML sampling and response parsing
    // -------------------------------------------------------------------------

    private static string ExtractRepresentativeSample(string html, string startMarker)
    {
        var start = html.IndexOf($"<{startMarker}", StringComparison.OrdinalIgnoreCase);

        if (start == -1)
        {
            foreach (var marker in new[] { "app-guideline-item", "jaspero-accord", "article" })
            {
                start = html.IndexOf($"<{marker}", StringComparison.OrdinalIgnoreCase);
                if (start != -1)
                {
                    Console.WriteLine($"[AI] Found sample starting at '{marker}'");
                    break;
                }
            }
        }
        else Console.WriteLine($"[AI] Found sample starting at '{startMarker}'");

        if (start == -1) return html[..Math.Min(3000, html.Length)];

        var end      = Math.Min(start + 6000, html.Length);
        var closeTag = html.LastIndexOf('>', end);
        var raw      = closeTag > start ? html[start..(closeTag + 1)] : html[start..end];

        return CleanAngularHtml(raw);
    }

    private static string CleanAngularHtml(string html)
    {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            html, @"\s_ngcontent-[a-z0-9\-]+""\s*=\s*""""", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"\s_nghost-[a-z0-9\-]+""\s*=\s*""""", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"<!---->\s*", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+ng-star-inserted", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+ng-tns-[a-z0-9\-]+", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+ng-trigger[a-z0-9\-]*", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(
            cleaned, @"\s+class=""jaspero__accord_inner[^""]*""", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s{2,}", " ");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @">\s+<", "><");
        return cleaned.Trim();
    }

    private static ExtractionPlan? ParseExtractionPlan(string response)
    {
        Console.WriteLine($"[AI] Raw extraction plan response: {response}");
        try
        {
            var cleaned = response.Trim();
            var start   = cleaned.IndexOf('{');
            var end     = cleaned.LastIndexOf('}');
            if (start == -1 || end == -1) return null;

            var plan = JsonSerializer.Deserialize<ExtractionPlan>(
                cleaned[start..(end + 1)],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (plan == null) return null;

            // Remove empty selectors — they crash the CSS parser
            plan.Fields = plan.Fields
                .Where(f => !string.IsNullOrWhiteSpace(f.Value))
                .ToDictionary(f => f.Key, f => f.Value);

            return plan;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AI] Failed to parse extraction plan: {ex.Message}");
            return null;
        }
    }
}