using Microsoft.Playwright;
using ScraperTemplate.Helpers;
using ScraperTemplate.Models;

namespace ScraperTemplate.Scraper;

/// <summary>
/// Autonomous scraper that uses a Hugging Face model to identify
/// document elements on unknown pages, with selector caching to
/// avoid repeated AI calls on subsequent runs.
/// </summary>
public class AutoScraper
{
    /// <summary>
    /// Optional pre-scrape action — use this to handle JS-heavy sites that need
    /// interaction before content is visible. Set in Program.cs per site.
    /// </summary>
    public Func<IPage, Task>? PreScrapeAction { get; set; }
    
    private readonly IPage _page;
    private readonly AiClient _ai;
    private readonly SelectorCache _cache;
    private readonly PageElementExtractor _extractor;
    
    public AutoScraper(IPage page, string apiKey, AiProvider provider = AiProvider.Ollama)
    {
        _page = page;
        _ai = new AiClient(apiKey, provider);
        _cache = new SelectorCache("selector_cache.json");
        _extractor = new PageElementExtractor(page);
    }

    /// <summary>
    /// Scrapes both guidelines and documents from the target URL.
    /// Uses cached selectors immediately and expands the cache via AI on each run.
    /// </summary>
    public async Task<(List<Guideline> Guidelines, List<GuidelineDocument> Documents)>
    ScrapeAsync(string url, string guidelineGoal, string documentGoal)
    {
        var allGuidelines = new List<Guideline>();
        var allDocuments = new List<GuidelineDocument>();
        var currentUrl = url;
        int pageNumber = 1;

        while (true)
        {
            Console.WriteLine($"\n[AutoScraper] Processing page {pageNumber}: {currentUrl}");

            await RetryHelper.ExecuteWithEscalationAsync(
                () => _page.GotoAsync(currentUrl, new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle }),
                _page, currentUrl, $"Loading page {pageNumber}");
            
            // After GotoAsync, before extraction:
            if (PreScrapeAction != null)
            {
                Console.WriteLine("[AutoScraper] Running pre-scrape action...");
                await PreScrapeAction(_page);
            }

            // Extract two separate candidate lists
            var linkCandidates = await _extractor.ExtractLinkCandidatesAsync();
            var contentCandidates = await _extractor.ExtractContentCandidatesAsync();
            Console.WriteLine($"[AutoScraper] Found {linkCandidates.Count} link candidates, " +
                              $"{contentCandidates.Count} content candidates");

            var guidelineCacheKey = $"{currentUrl}__guidelines";
            var documentCacheKey = $"{currentUrl}__documents";

            var cachedGuidelines = _cache.GetCachedCount(guidelineCacheKey);
            var cachedDocuments = _cache.GetCachedCount(documentCacheKey);
            Console.WriteLine($"[AutoScraper] Cache status — " +
                              $"guidelines: {cachedGuidelines}, documents: {cachedDocuments}");

            // Guidelines use content candidates + FindGuidelineElementsAsync
            await TryExpandGuidelineCacheAsync(
                guidelineCacheKey, contentCandidates, guidelineGoal);

            // Documents use link candidates + FindDocumentElementsAsync
            await TryExpandDocumentCacheAsync(
                documentCacheKey, linkCandidates, documentGoal);

            // Scrape from cache
            if (_cache.TryGet(guidelineCacheKey, out var guidelineElements))
            {
                var pageGuidelines = await ExtractGuidelinesFromSelectorsAsync(
                    guidelineElements, currentUrl);
                allGuidelines.AddRange(pageGuidelines);
                Console.WriteLine($"[AutoScraper] Page {pageNumber}: " +
                                  $"{pageGuidelines.Count} guidelines scraped");
            }
            else
                Console.WriteLine("[AutoScraper] No guideline selectors cached yet");

            if (_cache.TryGet(documentCacheKey, out var documentElements))
            {
                var pageDocs = await ExtractDocumentsFromSelectorsAsync(
                    documentElements, currentUrl);
                allDocuments.AddRange(pageDocs);
                Console.WriteLine($"[AutoScraper] Page {pageNumber}: " +
                                  $"{pageDocs.Count} documents scraped");
            }
            else
                Console.WriteLine("[AutoScraper] No document selectors cached yet");

            Console.WriteLine($"[AutoScraper] Running totals — " +
                              $"guidelines: {allGuidelines.Count}, " +
                              $"documents: {allDocuments.Count}");

            // Use link candidates for pagination
            var nextPage = await TryFindNextPageAsync(linkCandidates, currentUrl);
            if (nextPage == null || string.IsNullOrWhiteSpace(nextPage.Href))
            {
                Console.WriteLine("[AutoScraper] No next page found — scrape complete");
                break;
            }

            currentUrl = nextPage.Href.StartsWith("http")
                ? nextPage.Href
                : $"{new Uri(currentUrl).GetLeftPart(UriPartial.Authority)}{nextPage.Href}";
            pageNumber++;
        }

        return (allGuidelines, allDocuments);
    }

    private async Task TryExpandGuidelineCacheAsync(
        string cacheKey, List<ElementSummary> candidates, string goal)
    {
        try
        {
            Console.WriteLine($"[AutoScraper] Querying {_ai.ProviderName} for guidelines...");
            var found = await _ai.FindGuidelineElementsAsync(candidates, goal);
            if (found.Count > 0)
                _cache.Merge(cacheKey, found);
            else
                Console.WriteLine("[AutoScraper] AI found no new guideline elements");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            Console.WriteLine($"[AutoScraper] Rate limited — " +
                              $"using {_cache.GetCachedCount(cacheKey)} cached guideline selectors");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoScraper] AI error for guidelines — {ex.Message}");
        }
    }

    private async Task TryExpandDocumentCacheAsync(
        string cacheKey, List<ElementSummary> candidates, string goal)
    {
        try
        {
            Console.WriteLine($"[AutoScraper] Querying {_ai.ProviderName} for documents...");
            var found = await _ai.FindDocumentElementsAsync(candidates, goal);
            if (found.Count > 0)
                _cache.Merge(cacheKey, found);
            else
                Console.WriteLine("[AutoScraper] AI found no new document elements");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            Console.WriteLine($"[AutoScraper] Rate limited — " +
                              $"using {_cache.GetCachedCount(cacheKey)} cached document selectors");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoScraper] AI error for documents — {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Extraction from cached selectors
    // -------------------------------------------------------------------------

    private async Task<List<Guideline>> ExtractGuidelinesFromSelectorsAsync(
        List<ElementSummary> cachedElements, string currentUrl)
    {
        var guidelines = new List<Guideline>();
        var seenTexts = new HashSet<string>(); // prevent duplicates within a page

        foreach (var cached in cachedElements)
        {
            try
            {
                // A selector like "span.text" matches ALL quotes on the page
                // so we query all matching elements, not just the first
                var elements = await _page.QuerySelectorAllAsync(cached.Selector);

                foreach (var element in elements)
                {
                    if (!await element.IsVisibleAsync()) continue;

                    var text = (await element.InnerTextAsync()).Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    // Skip duplicates
                    if (!seenTexts.Add(text)) continue;

                    guidelines.Add(new Guideline
                    {
                        GuidelineCode = "",
                        Title = text,
                        Category = "",
                        Step = "",
                        Status = "",
                        Dated = DateTime.MinValue,
                        Summary = "",
                        SourceUrl = currentUrl
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[AutoScraper] Guideline selector failed: {cached.Selector} — {ex.Message}");
            }
        }

        return guidelines;
    }

    private async Task<List<GuidelineDocument>> ExtractDocumentsFromSelectorsAsync(
        List<ElementSummary> cachedElements, string currentUrl)
    {
        var documents = new List<GuidelineDocument>();

        foreach (var cached in cachedElements)
        {
            try
            {
                var element = await _page.QuerySelectorAsync(cached.Selector);
                if (element == null || !await element.IsVisibleAsync()) continue;

                var text = (await element.InnerTextAsync()).Trim();
                var href = await element.GetAttributeAsync("href") ?? cached.Href;
                var cssClass = await element.GetAttributeAsync("class") ?? cached.CssClass;

                if (string.IsNullOrWhiteSpace(href)) continue;

                var docUrl = href.StartsWith("http")
                    ? href
                    : $"{new Uri(currentUrl).GetLeftPart(UriPartial.Authority)}{href}";

                documents.Add(new GuidelineDocument
                {
                    GuidelineCode = "",
                    DocumentTitle = text,
                    DocumentUrl = docUrl,
                    DocumentType = InferDocumentType(cssClass, href),
                    FileFormat = InferFileFormat(cssClass, href)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[AutoScraper] Document selector failed: {cached.Selector} — {ex.Message}");
            }
        }

        return documents;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string InferDocumentType(string cssClass, string href)
    {
        if (cssClass.Contains("pdf")) return "PDF Document";
        if (href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return "PDF Document";
        if (cssClass.Contains("link")) return "Web Page";
        return "Document";
    }

    private static string InferFileFormat(string cssClass, string href)
    {
        if (cssClass.Contains("pdf")) return "PDF";
        var ext = Path.GetExtension(href).TrimStart('.').ToUpper();
        return string.IsNullOrWhiteSpace(ext) ? "HTML" : ext;
    }
    
    private async Task<ElementSummary?> TryFindNextPageAsync(
    List<ElementSummary> candidates, string currentUrl)
{
    try
    {
        var baseHost = new Uri(currentUrl).Host;

        // Try direct CSS selector first — much more reliable than AI for pagination
        // Common next page patterns across most sites
        var nextSelectors = new[]
        {
            "li.next a",           // quotes.toscrape.com
            "a[rel='next']",       // semantic HTML
            ".next a",
            ".pagination .next",
            "a:has(~ .sr-only:contains('Next'))",
            "[aria-label='Next page']",
            "[aria-label='Next']",
        };

        foreach (var selector in nextSelectors)
        {
            try
            {
                var element = await _page.QuerySelectorAsync(selector);
                if (element == null || !await element.IsVisibleAsync()) continue;

                var href = await element.GetAttributeAsync("href") ?? "";
                var text = (await element.InnerTextAsync()).Trim();

                if (string.IsNullOrWhiteSpace(href)) continue;

                // Reject external domains
                try
                {
                    var nextHost = new Uri(href).Host;
                    if (nextHost != baseHost && !string.IsNullOrWhiteSpace(nextHost))
                        continue;
                }
                catch { /* relative URL — safe */ }

                Console.WriteLine($"[AutoScraper] Next page found via selector " +
                                  $"'{selector}': {text} → {href}");

                return new ElementSummary
                {
                    Text = text,
                    Href = href,
                    Selector = selector
                };
            }
            catch { /* try next selector */ }
        }

        // Fall back to AI if direct selectors fail
        Console.WriteLine("[AutoScraper] No next page via CSS — trying AI...");
        var nextPage = await _ai.FindNextPageElementAsync(candidates);
        if (nextPage == null || string.IsNullOrWhiteSpace(nextPage.Href))
            return null;

        // Validate domain
        try
        {
            var nextHost = new Uri(nextPage.Href).Host;
            if (nextHost != baseHost && !string.IsNullOrWhiteSpace(nextHost))
            {
                Console.WriteLine($"[AutoScraper] Rejected AI next page — " +
                                  $"external domain: {nextHost}");
                return null;
            }
        }
        catch { /* relative URL — safe */ }

        return nextPage;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[AutoScraper] Could not determine next page — {ex.Message}");
        return null;
    }
}
}