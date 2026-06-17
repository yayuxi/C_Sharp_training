using Microsoft.Playwright;
using ScraperTemplate.Helpers;
using ScraperTemplate.Models;

namespace ScraperTemplate.Scraper;

public class AutoScraper
{
    private readonly IPage _page;
    private readonly AiClient _ai;
    private readonly SelectorCache _cache;
    private readonly PageElementExtractor _extractor;

    public AutoScraper(IPage page, string apiKey, AiProvider provider = AiProvider.Groq)
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

            var candidates = await _extractor.ExtractCandidatesAsync();
            Console.WriteLine($"[AutoScraper] Found {candidates.Count} candidate elements");

            // Use two separate cache keys — one for guidelines, one for documents
            var guidelineCacheKey = $"{currentUrl}__guidelines";
            var documentCacheKey = $"{currentUrl}__documents";

            // Report what's already cached
            var cachedGuidelines = _cache.GetCachedCount(guidelineCacheKey);
            var cachedDocuments = _cache.GetCachedCount(documentCacheKey);
            Console.WriteLine($"[AutoScraper] Cache status — " +
                              $"guidelines: {cachedGuidelines}, documents: {cachedDocuments}");

            // Try to expand both caches via AI
            await TryExpandCacheAsync(guidelineCacheKey, candidates, guidelineGoal, "guidelines");
            await TryExpandCacheAsync(documentCacheKey, candidates, documentGoal, "documents");

            // Scrape guidelines from cache
            if (_cache.TryGet(guidelineCacheKey, out var guidelineElements))
            {
                var pageGuidelines = await ExtractGuidelinesFromSelectorsAsync(
                    guidelineElements, currentUrl);
                allGuidelines.AddRange(pageGuidelines);
                Console.WriteLine($"[AutoScraper] Page {pageNumber}: " +
                                  $"{pageGuidelines.Count} guidelines scraped");
            }
            else
            {
                Console.WriteLine("[AutoScraper] No guideline selectors cached yet " +
                                  "— run again to accumulate more");
            }

            // Scrape documents from cache
            if (_cache.TryGet(documentCacheKey, out var documentElements))
            {
                var pageDocs = await ExtractDocumentsFromSelectorsAsync(
                    documentElements, currentUrl);
                allDocuments.AddRange(pageDocs);
                Console.WriteLine($"[AutoScraper] Page {pageNumber}: " +
                                  $"{pageDocs.Count} documents scraped");
            }
            else
            {
                Console.WriteLine("[AutoScraper] No document selectors cached yet " +
                                  "— run again to accumulate more");
            }

            Console.WriteLine($"[AutoScraper] Running totals — " +
                              $"guidelines: {allGuidelines.Count}, " +
                              $"documents: {allDocuments.Count}");

            // Check for next page
            var nextPage = await TryFindNextPageAsync(candidates);
            if (nextPage == null)
            {
                Console.WriteLine("[AutoScraper] No next page found — done");
                break;
            }

            currentUrl = nextPage.Href.StartsWith("http")
                ? nextPage.Href
                : $"{new Uri(currentUrl).GetLeftPart(UriPartial.Authority)}{nextPage.Href}";
            pageNumber++;
        }

        return (allGuidelines, allDocuments);
    }

    // -------------------------------------------------------------------------
    // Cache expansion
    // -------------------------------------------------------------------------

    private async Task TryExpandCacheAsync(
        string cacheKey,
        List<ElementSummary> candidates,
        string goal,
        string label)
    {
        try
        {
            Console.WriteLine($"[AutoScraper] Querying {_ai.ProviderName} for {label}...");
            var found = await _ai.FindDocumentElementsAsync(candidates, goal);

            if (found.Count > 0)
                _cache.Merge(cacheKey, found);
            else
                Console.WriteLine($"[AutoScraper] AI found no new {label} elements");
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("429"))
        {
            Console.WriteLine($"[AutoScraper] Rate limited on {label} query — " +
                              $"using {_cache.GetCachedCount(cacheKey)} cached selectors");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoScraper] AI error for {label} — {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Extraction from cached selectors
    // -------------------------------------------------------------------------

    private async Task<List<Guideline>> ExtractGuidelinesFromSelectorsAsync(
        List<ElementSummary> cachedElements, string currentUrl)
    {
        var guidelines = new List<Guideline>();

        foreach (var cached in cachedElements)
        {
            try
            {
                var element = await _page.QuerySelectorAsync(cached.Selector);
                if (element == null || !await element.IsVisibleAsync()) continue;

                var text = (await element.InnerTextAsync()).Trim();
                var href = await element.GetAttributeAsync("href") ?? cached.Href;

                if (string.IsNullOrWhiteSpace(text)) continue;

                var sourceUrl = href.StartsWith("http")
                    ? href
                    : $"{new Uri(currentUrl).GetLeftPart(UriPartial.Authority)}{href}";

                guidelines.Add(new Guideline
                {
                    GuidelineCode = "",   // AI can't reliably extract these
                    Title = text,         // from a simple element summary —
                    Category = "",        // for full field extraction the manual
                    Step = "",            // scraper is still needed
                    Status = "",
                    Dated = DateTime.MinValue,
                    Summary = "",
                    SourceUrl = sourceUrl
                });
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
    // Pagination
    // -------------------------------------------------------------------------

    private async Task<ElementSummary?> TryFindNextPageAsync(List<ElementSummary> candidates)
    {
        try { return await _ai.FindNextPageElementAsync(candidates); }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoScraper] Could not determine next page — {ex.Message}");
            return null;
        }
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
}