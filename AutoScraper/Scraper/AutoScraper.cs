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
    private readonly IPage _page;
    private readonly AiClient _ai;
    private readonly SelectorCache _cache;
    private readonly PageElementExtractor _extractor;
    
    public AutoScraper(IPage page, string apiKey, AiProvider provider = AiProvider.HuggingFace)
    {
        _page = page;
        _ai = new AiClient(apiKey, provider);
        _cache = new SelectorCache();
        _extractor = new PageElementExtractor(page);
    }

    /// <summary>
    /// Main entry point — navigates to the URL, finds documents using AI,
    /// handles pagination, and returns all found document elements.
    /// </summary>
    public async Task<List<GuidelineDocument>> ScrapeDocumentsAsync(
        string url,
        string goal = "regulatory documents, guidelines, or PDF files")
    {
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

            // Extract all candidate elements from the page
            var candidates = await _extractor.ExtractCandidatesAsync();
            Console.WriteLine($"[AutoScraper] Found {candidates.Count} candidate elements");

            // Get document elements — from cache or AI
            var documentElements = await GetDocumentElementsAsync(currentUrl, candidates, goal);
            Console.WriteLine($"[AutoScraper] Identified {documentElements.Count} document elements");

            // Convert to GuidelineDocument records
            var pageDocs = documentElements.Select(e => new GuidelineDocument
            {
                GuidelineCode = "",  // unknown without deeper context
                DocumentTitle = e.Text,
                DocumentUrl = e.Href.StartsWith("http")
                    ? e.Href
                    : $"{new Uri(currentUrl).GetLeftPart(UriPartial.Authority)}{e.Href}",
                DocumentType = InferDocumentType(e),
                FileFormat = InferFileFormat(e)
            }).ToList();

            allDocuments.AddRange(pageDocs);
            Console.WriteLine($"[AutoScraper] Page {pageNumber}: {pageDocs.Count} documents — " +
                              $"Total: {allDocuments.Count}");

            // Check for next page
            var nextPage = await _ai.FindNextPageElementAsync(candidates);
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

        return allDocuments;
    }

    private async Task<List<ElementSummary>> GetDocumentElementsAsync(
        string url,
        List<ElementSummary> candidates,
        string goal)
    {
        if (_cache.TryGet(url, out var cached))
        {
            if (await ValidateCachedSelectorsAsync(cached))
                return cached;

            Console.WriteLine("[AutoScraper] Cached selectors invalid — re-querying AI");
            _cache.Invalidate(url);
        }

        try
        {
            Console.WriteLine("[AutoScraper] Querying Hugging Face model...");
            var found = await _ai.FindDocumentElementsAsync(candidates, goal);

            if (found.Count > 0)
                _cache.Set(url, found);

            return found;
        }
        catch (ScraperException ex)
        {
            // AI unavailable — fall back to returning all candidates
            // so the scraper still produces something useful
            Console.WriteLine($"[AutoScraper] AI query failed — {ex.Message}");
            Console.WriteLine("[AutoScraper] Falling back to returning all candidates");
            return candidates;
        }
    }

    /// <summary>
    /// Checks that at least one cached selector still finds an element on the page.
    /// If the site restructured, selectors will be stale.
    /// </summary>
    private async Task<bool> ValidateCachedSelectorsAsync(List<ElementSummary> cached)
    {
        foreach (var item in cached.Take(3)) // check first 3 as a sample
        {
            var element = await _page.QuerySelectorAsync(item.Selector);
            if (element != null && await element.IsVisibleAsync())
                return true;
        }
        return false;
    }

    private static string InferDocumentType(ElementSummary element)
    {
        if (element.CssClass.Contains("pdf")) return "PDF Document";
        if (element.Href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return "PDF Document";
        if (element.CssClass.Contains("link")) return "Web Page";
        return "Document";
    }

    private static string InferFileFormat(ElementSummary element)
    {
        if (element.CssClass.Contains("pdf")) return "PDF";
        var ext = Path.GetExtension(element.Href).TrimStart('.').ToUpper();
        return string.IsNullOrWhiteSpace(ext) ? "HTML" : ext;
    }
}