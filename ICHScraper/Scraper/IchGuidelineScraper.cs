using IchScraper.Helpers;
using IchScraper.Models;
using Microsoft.Playwright;

namespace IchScraper.Scraper;

public class IchGuidelineScraper
{
    private readonly IPage _page;
    private const string BaseUrl = "https://www.ich.org";

    public IchGuidelineScraper(IPage page)
    {
        _page = page;
    }

    public async Task<(List<Guideline> Guidelines, List<GuidelineDocument> Documents)>
        ScrapeCategoryAsync(string categoryName, string categoryUrl)
    {
        var guidelines = new List<Guideline>();
        var documents = new List<GuidelineDocument>();

        await RetryHelper.ExecuteAsync(
            () => _page.GotoAsync(categoryUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }),
            $"Loading category: {categoryName}");

        await _page.WaitForSelectorAsync("jaspero-accordion");

        // Outer accordion: each jaspero-accord is a topic group e.g. "Q1 Stability"
        var outerAccords = await _page.QuerySelectorAllAsync(
            "app-accordion > jaspero-accordion > jaspero-accord");
        Console.WriteLine($"[{categoryName}] Found {outerAccords.Count} topic groups");

        int groupNumber = 0;
        foreach (var outerAccord in outerAccords)
        {
            groupNumber++;

            // Click outer header (div[1]) to expand the group
            var outerHeader = await outerAccord.QuerySelectorAsync("div:first-child");
            if (outerHeader != null)
            {
                await outerHeader.ClickAsync();
                await _page.WaitForTimeoutAsync(600);
            }

            // Inner accordion: each jaspero-accord is one individual guideline
            var innerAccords = await outerAccord.QuerySelectorAllAsync(
                "jaspero-accordion > jaspero-accord");
            Console.WriteLine($"[{categoryName}] Group {groupNumber}: {innerAccords.Count} guidelines");

            foreach (var innerAccord in innerAccords)
            {
                try
                {
                    // --- Code and Title are in the header div[1], not in app-accordion-guidline ---
                    // jaspero-accord > div:first-child > jaspero-variable-content > div > section
                    var headerSection = await innerAccord.QuerySelectorAsync(
                        "div:first-child jaspero-variable-content div section");

                    var codeElement = await headerSection?.QuerySelectorAsync("span:nth-child(1)");
                    var titleElement = await headerSection?.QuerySelectorAsync("span:nth-child(2)");

                    var code = codeElement != null
                        ? (await codeElement.InnerTextAsync()).Trim()
                        : "";
                    var title = titleElement != null
                        ? (await titleElement.InnerTextAsync()).Trim()
                        : "";

                    // Click inner header to expand guideline content
                    var innerHeader = await innerAccord.QuerySelectorAsync("div:first-child");
                    if (innerHeader != null)
                    {
                        await innerHeader.ClickAsync();
                        await _page.WaitForTimeoutAsync(400);
                    }

                    // Content is inside app-accordion-guidline > div
                    var content = await innerAccord.QuerySelectorAsync(
                        "app-accordion-guidline > div");
                    if (content == null)
                    {
                        Console.WriteLine($"[Warning] No content found for guideline {code}");
                        continue;
                    }

                    var guideline = await ScrapeGuidelineContentAsync(
                        content, code, title, categoryName);
                    var guidelineDocuments = await ScrapeDocumentsAsync(content, code);

                    if (guideline != null)
                    {
                        guidelines.Add(guideline);
                        documents.AddRange(guidelineDocuments);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"[Warning] Failed to scrape guideline in {categoryName} " +
                        $"group {groupNumber} — {ex.Message}");
                }
            }

            Console.WriteLine($"[{categoryName}] Done group {groupNumber}/{outerAccords.Count}");
        }

        return (guidelines, documents);
    }

    private async Task<Guideline?> ScrapeGuidelineContentAsync(
        IElementHandle content, string code, string title, string category)
    {
        // Summary: section[1] > div[1] > p > p
        var summaryElement = await content.QuerySelectorAsync(
            "section:nth-child(1) > div:nth-child(1) p p");
        // Fallback if inner <p> not present
        summaryElement ??= await content.QuerySelectorAsync(
            "section:nth-child(1) > div:nth-child(1) p");
        var summary = summaryElement != null
            ? (await summaryElement.InnerTextAsync()).Trim()
            : "";

        // Date published: section[1] > div[2] > div[1] > p[2]
        var datedElement = await content.QuerySelectorAsync(
            "section:nth-child(1) > div:nth-child(2) > div:nth-child(1) > p:nth-child(2)");
        var datedText = datedElement != null
            ? (await datedElement.InnerTextAsync()).Trim()
            : "";
        DateTime.TryParse(datedText, out var dated);

        // Step: div[2] for step 5 guidelines, div[3] for others — try both
        var stepElement = await content.QuerySelectorAsync(
            "section:nth-child(1) > div:nth-child(1) > div:nth-child(2) p em, " +
            "section:nth-child(1) > div:nth-child(2) > div:nth-child(2) p em, " +
            "section:nth-child(1) > div:nth-child(3) > div:nth-child(1) p em, " +
            "section:nth-child(1) > div:nth-child(3) > div:nth-child(2) p em");
        var step = stepElement != null
            ? (await stepElement.InnerTextAsync()).Trim()
            : "";

        // Status: Finalized | Under Development | Retired;
        var status = DeriveStatus(step);

        // Source URL: first link in section[1] that points to a guideline page
        var sourceLink = await content.QuerySelectorAsync(
            "section:nth-child(1) a[href*='/guideline'], section:nth-child(1) a[href*='/page']");
        var sourceHref = sourceLink != null
            ? await sourceLink.GetAttributeAsync("href") ?? ""
            : "";
        var sourceUrl = sourceHref.StartsWith("http")
            ? sourceHref
            : $"{BaseUrl}{sourceHref}";

        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(title))
            return null;

        return new Guideline
        {
            GuidelineCode = code,
            Title = title,
            Category = category,
            Step = step,
            Status = status,
            Dated = dated,
            Summary = summary,
            SourceUrl = sourceUrl
        };
    }

    private static string DeriveStatus(string step)
    {
        if (string.IsNullOrWhiteSpace(step)) return "Retired";
        if (step.Contains("5")) return "Finalised";
        return "Under Development";
    }
    private async Task<List<GuidelineDocument>> ScrapeDocumentsAsync(
        IElementHandle content, string guidelineCode)
    {
        var documents = new List<GuidelineDocument>();

        // There are multiple app-file-accordion elements in section[2]
        // each representing a document group with its own title and one or more links
        var fileAccordions = await content.QuerySelectorAllAsync(
            "section:nth-child(2) app-file-accordion");

        foreach (var accordion in fileAccordions)
        {
            // The group title (e.g. "Step 4 Guideline", "Questions & Answers") 
            // is typically in the article header before the links
            var groupTitleElement = await accordion.QuerySelectorAsync(
                "article header, article h4, article h3, article > div > span, article > span");
            var groupTitle = groupTitleElement != null
                ? (await groupTitleElement.InnerTextAsync()).Trim()
                : "";

            // All PDF/document links within this accordion group
            var links = await accordion.QuerySelectorAllAsync("article div a");

            foreach (var link in links)
            {
                var docTitle = (await link.InnerTextAsync()).Trim();
                var href = await link.GetAttributeAsync("href") ?? "";
                var docUrl = href.StartsWith("http") ? href : $"{BaseUrl}{href}";
                var cssClass = await link.GetAttributeAsync("class") ?? "";

                // Derive format from CSS class: "document-pdf" → PDF, "document-link" → HTML
                // Fall back to parsing the href extension for any other types
                var fileFormat = cssClass switch
                {
                    var c when c.Contains("document-pdf")  => "PDF",
                    var c when c.Contains("document-link") => "HTML",
                    _ => Path.GetExtension(href).TrimStart('.').ToUpper() // e.g. ".docx" → "DOCX"
                };

                if (string.IsNullOrWhiteSpace(docTitle)) continue;

                documents.Add(new GuidelineDocument
                {
                    GuidelineCode = guidelineCode,
                    DocumentTitle = docTitle,
                    DocumentType = groupTitle,
                    DocumentUrl = docUrl,
                    FileFormat = fileFormat
                });
            }
        }

        return documents;
    }
}