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
    private ExtractionPlan? _guidelinePlan;
    private ExtractionPlan? _documentPlan;

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

            if (PreScrapeAction != null)
            {
                Console.WriteLine("[AutoScraper] Running pre-scrape action...");
                await PreScrapeAction(_page);
            }

            // Get the rendered HTML after JS and accordions have loaded
            var pageHtml = await _page.ContentAsync();

            // Ask AI to build extraction plans if we don't have them yet
            if (_guidelinePlan == null)
            {
                Console.WriteLine("[AutoScraper] Asking AI to analyse page for guidelines...");
                _guidelinePlan = await TryGetExtractionPlanAsync(
                    pageHtml, guidelineGoal, "app-accordion-guidline");
            }

            if (_documentPlan == null)
            {
                Console.WriteLine("[AutoScraper] Asking AI to analyse page for documents...");
                _documentPlan = await TryGetExtractionPlanAsync(
                    pageHtml, documentGoal, "app-file-accordion");
            }

            // Extract guidelines using the AI's plan
            if (_guidelinePlan != null)
            {
                var pageGuidelines = await ExtractWithPlanAsync(
                    _guidelinePlan, currentUrl, isGuideline: true);
                allGuidelines.AddRange(pageGuidelines.Cast<Guideline>());
                Console.WriteLine($"[AutoScraper] Page {pageNumber}: " +
                                  $"{pageGuidelines.Count} guidelines scraped");
            }
            else
                Console.WriteLine("[AutoScraper] No guideline plan yet — run again");

            // Extract documents using the AI's plan
            if (_documentPlan != null)
            {
                var pageDocs = await ExtractWithPlanAsync(
                    _documentPlan, currentUrl, isGuideline: false);
                allDocuments.AddRange(pageDocs.Cast<GuidelineDocument>());
                Console.WriteLine($"[AutoScraper] Page {pageNumber}: " +
                                  $"{pageDocs.Count} documents scraped");
            }
            else
                Console.WriteLine("[AutoScraper] No document plan yet — run again");

            Console.WriteLine($"[AutoScraper] Running totals — " +
                              $"guidelines: {allGuidelines.Count}, " +
                              $"documents: {allDocuments.Count}");

            var linkCandidates = await _extractor.ExtractLinkCandidatesAsync();
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

    private async Task<ExtractionPlan?> TryGetExtractionPlanAsync(
        string pageHtml, string goal, string sampleHint = "")
    {
        try
        {
            var plan = await _ai.AnalysePageStructureAsync(pageHtml, goal, sampleHint);
            if (plan == null || !plan.IsValid)
            {
                Console.WriteLine("[AutoScraper] AI returned invalid plan");
                return null;
            }

            // Validate container selector actually finds elements on the page
            var containers = await _page.QuerySelectorAllAsync(plan.ContainerSelector);
            if (containers.Count == 0)
            {
                Console.WriteLine($"[AutoScraper] Plan rejected — container " +
                                  $"'{plan.ContainerSelector}' found 0 elements on page");

                // Log what classes ARE available to help debug
                var availableClasses = await _page.EvaluateAsync<string>("""
                    () => {
                        const classes = new Set();
                        document.querySelectorAll('[class]').forEach(el => {
                            el.className.trim().split(/\s+/).forEach(c => {
                                if (c && !c.includes('ng-') && !c.includes('cdk-'))
                                    classes.add(c);
                            });
                        });
                        return [...classes].slice(0, 30).join(', ');
                    }
                """);
                Console.WriteLine($"[AutoScraper] Available classes on page: {availableClasses}");
                return null;
            }

            // Validate at least one field selector finds something
            var firstContainer = containers[0];
            var workingFields = new Dictionary<string, string>();
            foreach (var (fieldName, selector) in plan.Fields)
            {
                var element = await firstContainer.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var text = (await element.InnerTextAsync()).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        workingFields[fieldName] = selector;
                        Console.WriteLine($"[AutoScraper] Field '{fieldName}' works: '{text[..Math.Min(50, text.Length)]}'");
                    }
                    else
                        Console.WriteLine($"[AutoScraper] Field '{fieldName}' selector found element but text is empty");
                }
                else
                    Console.WriteLine($"[AutoScraper] Field '{fieldName}' selector '{selector}' found nothing");
            }
            
            // After validating the plan, try a known fallback for Step if missing
            if (sampleHint.Contains("guidline", StringComparison.OrdinalIgnoreCase) &&
                !workingFields.ContainsKey("Step"))
            {
                var stepFallbackSelectors = new[]
                {
                    "div:nth-child(2) p em",
                    "div:nth-child(3) p em",
                    "em"
                };

                foreach (var stepSelector in stepFallbackSelectors)
                {
                    var stepElement = await containers[0].QuerySelectorAsync(stepSelector);
                    if (stepElement != null)
                    {
                        var stepText = (await stepElement.InnerTextAsync()).Trim();
                        if (!string.IsNullOrWhiteSpace(stepText))
                        {
                            workingFields["Step"] = stepSelector;
                            Console.WriteLine($"[AutoScraper] Step fallback found via '{stepSelector}': '{stepText}'");
                            break;
                        }
                    }
                }
            }

            if (workingFields.Count == 0)
            {
                Console.WriteLine("[AutoScraper] Plan rejected — no field selectors work");
                return null;
            }

            // Return plan with only the working fields
            plan.Fields = workingFields;
            Console.WriteLine($"[AutoScraper] Plan validated — {containers.Count} containers, " +
                              $"{workingFields.Count} working fields");
            return plan;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoScraper] Failed to get extraction plan — {ex.Message}");
            return null;
        }
    }

    private async Task<List<object>> ExtractWithPlanAsync(
        ExtractionPlan plan, string currentUrl, bool isGuideline)
    {
        var results = new List<object>();

        try
        {
            var containers = await _page.QuerySelectorAllAsync(plan.ContainerSelector);
            Console.WriteLine($"[AutoScraper] Found {containers.Count} containers " +
                              $"with selector '{plan.ContainerSelector}'");

            var seenValues = new HashSet<string>();

            foreach (var container in containers)
            {
                try
                {
                    var fields = new Dictionary<string, string>();
                    foreach (var (fieldName, selector) in plan.Fields)
                    {
                        try
                        {
                            var element = await container.QuerySelectorAsync(selector);
                            if (element != null)
                            {
                                var raw = (await element.InnerTextAsync()).Trim();
                                fields[fieldName] = CleanFieldValue(raw);
                            }
                            else fields[fieldName] = "";
                        }
                        catch { fields[fieldName] = ""; }
                    }

                    if (fields.Values.All(string.IsNullOrWhiteSpace)) continue;

                    if (isGuideline)
                    {
                        // Code and title always come from the accordion header,
                        // never from the AI-discovered container, since they
                        // live outside the sample the AI was shown
                        var (code, title) = await GetCodeAndTitleFromHeaderAsync(container);

                        var summary = GetFieldFlexible(fields, "summary", "guidelinedescription", "description");
                        var dateText = GetFieldFlexible(fields, "date", "dated");
                        var step = GetFieldFlexible(fields, "step", "status");

                        DateTime.TryParse(dateText, out var dated);

                        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(title))
                            continue;

                        var fingerprint = code + "|" + title;
                        if (!seenValues.Add(fingerprint)) continue;

                        results.Add(new Guideline
                        {
                            GuidelineCode = code,
                            Title = title,
                            Step = step,
                            Status = DeriveStatus(step),
                            Dated = dated,
                            Summary = summary,
                            SourceUrl = currentUrl
                        });
                    }
                    else
                    {
                        // unchanged document logic
                        var href = "";
                        try
                        {
                            var linkElement = await container.QuerySelectorAsync("a[href]");
                            if (linkElement != null)
                            {
                                href = await linkElement.GetAttributeAsync("href") ?? "";
                                if (string.IsNullOrWhiteSpace(GetFieldFlexible(fields, "title")))
                                    fields["Title"] = (await linkElement.InnerTextAsync()).Trim();
                            }
                        }
                        catch { }

                        var docUrl = href.StartsWith("http")
                            ? href
                            : string.IsNullOrWhiteSpace(href)
                                ? currentUrl
                                : $"{new Uri(currentUrl).GetLeftPart(UriPartial.Authority)}{href}";

                        var cssClass = "";
                        try
                        {
                            var linkEl = await container.QuerySelectorAsync("a");
                            cssClass = await linkEl?.GetAttributeAsync("class") ?? "";
                        }
                        catch { }

                        var rawTitle = GetFieldFlexible(fields, "title")
                            .IfEmpty(fields.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "");

                        var codeMatch = System.Text.RegularExpressions.Regex.Match(
                            rawTitle, @"^([A-Z]\d+[A-Z]?(?:\([A-Z0-9]+\))?)\s+(.+)$");
                        var docCode = codeMatch.Success ? codeMatch.Groups[1].Value : "";
                        var docTitle = codeMatch.Success ? codeMatch.Groups[2].Value : rawTitle;

                        var fingerprint = docUrl;
                        if (!seenValues.Add(fingerprint)) continue;

                        results.Add(new GuidelineDocument
                        {
                            GuidelineCode = docCode,
                            DocumentTitle = docTitle,
                            DocumentUrl = docUrl,
                            DocumentType = GetFieldFlexible(fields, "type").IfEmpty("Document"),
                            FileFormat = InferFileFormat(cssClass, docUrl)
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AutoScraper] Container extraction failed — {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoScraper] Plan extraction failed — {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Looks up a field by trying several possible key variations case-insensitively,
    /// since the AI may return slightly different field names each time.
    /// </summary>
    private static string GetFieldFlexible(Dictionary<string, string> fields, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var match = fields.FirstOrDefault(f =>
                f.Key.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
                f.Key.Contains(candidate, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
                return match.Value;
        }
        return "";
    }

    /// <summary>
    /// Gets the guideline code and title from the accordion header,
    /// which is structurally outside the AI's sample container.
    /// This mirrors the known ICH structure: jaspero-variable-content > div > section > span[1], span[2]
    /// </summary>
    private async Task<(string Code, string Title)> GetCodeAndTitleFromHeaderAsync(
        IElementHandle container)
    {
        try
        {
            var result = await container.EvaluateAsync<string>("""
                el => {
                    let node = el;
                    // Walk up to find the jaspero-accord ancestor
                    while (node && node.tagName?.toLowerCase() !== 'jaspero-accord')
                        node = node.parentElement;
                    if (!node) return '';

                    const header = node.querySelector(
                        'div:first-child jaspero-variable-content div section');
                    if (!header) return '';

                    const spans = header.querySelectorAll('span');
                    if (spans.length === 0) return '';
                    const code = spans[0]?.innerText.trim() ?? '';
                    const title = spans.length > 1 ? spans[1].innerText.trim() : '';
                    return code + '|||' + title;
                }
            """);

            var parts = result.Split("|||");
            return (parts.Length > 0 ? parts[0] : "", parts.Length > 1 ? parts[1] : "");
        }
        catch
        {
            return ("", "");
        }
    }
    
    private static string CleanFieldValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        // Remove common label prefixes ending with colon
        // e.g. "Date of Step 4:\n6 February 2003" → "6 February 2003"
        // e.g. "Status: Step 5" → "Step 5"
        var colonIndex = value.IndexOf(':');
        if (colonIndex > 0 && colonIndex < 40)
        {
            var afterColon = value[(colonIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(afterColon))
                return afterColon;
        }

        return value.Trim();
    }

    private static string DeriveStatus(string step)
    {
        if (string.IsNullOrWhiteSpace(step)) return "Retired";
        if (step.Contains("5")) return "Finalised";
        return "Under Development";
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