using Microsoft.Playwright;

namespace ScraperTemplate.Helpers;

public class PageElementExtractor
{
    private readonly IPage _page;

    public PageElementExtractor(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Extracts links and buttons — used for finding documents and navigation.
    /// </summary>
    public async Task<List<ElementSummary>> ExtractLinkCandidatesAsync()
    {
        var elements = await _page.QuerySelectorAllAsync("a[href], button");
        var summaries = new List<ElementSummary>();
        int index = 1;

        foreach (var element in elements)
        {
            try
            {
                if (!await element.IsVisibleAsync()) continue;

                var text = (await element.InnerTextAsync()).Trim();
                var href = await element.GetAttributeAsync("href") ?? "";
                var cssClass = await element.GetAttributeAsync("class") ?? "";
                var tagName = await element.EvaluateAsync<string>(
                    "el => el.tagName.toLowerCase()");

                if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(href))
                    continue;
                if (IsNavigationalNoise(text, href, cssClass)) continue;

                var parentClass = await element.EvaluateAsync<string>(
                    "el => el.parentElement?.className ?? ''");
                var parentText = (await element.EvaluateAsync<string>(
                    "el => el.parentElement?.innerText ?? ''"))
                    .Split('\n')[0].Trim();

                var selector = await BuildSelectorAsync(element, href, cssClass);

                summaries.Add(new ElementSummary
                {
                    Index = index++,
                    Text = text.Length > 80 ? text[..80] : text,
                    Href = href,
                    CssClass = cssClass,
                    TagName = tagName,
                    ParentText = parentText.Length > 60 ? parentText[..60] : parentText,
                    ParentClass = parentClass,
                    Selector = selector
                });
            }
            catch { /* skip */ }
        }

        return summaries;
    }

    /// <summary>
    /// Extracts text content elements — used for finding guidelines, quotes,
    /// titles, or any non-link content that needs to be scraped as data.
    /// </summary>
    public async Task<List<ElementSummary>> ExtractContentCandidatesAsync()
    {
        // Target only the most specific content elements, not their parents
        // On quotes.toscrape.com the quote text is in span.text, author in small.author
        var elements = await _page.QuerySelectorAllAsync(
            "span.text, small.author, h1, h2, h3, " +
            ".quote .text, .quote .author, " +
            "blockquote p, article p, .content p");

        var summaries = new List<ElementSummary>();
        var seenTexts = new HashSet<string>(); // deduplicate by text content
        int index = 1;

        foreach (var element in elements)
        {
            try
            {
                if (!await element.IsVisibleAsync()) continue;

                var text = (await element.InnerTextAsync()).Trim();
                if (string.IsNullOrWhiteSpace(text) || text.Length < 10) continue;
                if (IsContentNoise(text, await element.GetAttributeAsync("class") ?? ""))
                    continue;

                // Skip if we've already seen this exact text — prevents parent/child duplication
                if (!seenTexts.Add(text)) continue;

                var cssClass = await element.GetAttributeAsync("class") ?? "";
                var tagName = await element.EvaluateAsync<string>(
                    "el => el.tagName.toLowerCase()");
                var parentClass = await element.EvaluateAsync<string>(
                    "el => el.parentElement?.className ?? ''");

                var selector = await element.EvaluateAsync<string>("""
                    el => {
                        const tag = el.tagName.toLowerCase();
                        if (el.className) {
                            const firstClass = el.className.trim().split(' ')[0];
                            return tag + '.' + firstClass;
                        }
                        if (el.id) return '#' + el.id;
                        const parent = el.parentElement;
                        if (parent?.className) {
                            const parentClass = parent.className.trim().split(' ')[0];
                            return '.' + parentClass + ' ' + tag;
                        }
                        return tag;
                    }
                """);

                summaries.Add(new ElementSummary
                {
                    Index = index++,
                    Text = text.Length > 120 ? text[..120] : text,
                    Href = "",
                    CssClass = cssClass,
                    TagName = tagName,
                    ParentText = "",
                    ParentClass = parentClass,
                    Selector = selector
                });
            }
            catch { /* skip */ }
        }

        return summaries;
    }

    // Keep the old method name as an alias for backwards compatibility
    public async Task<List<ElementSummary>> ExtractCandidatesAsync()
        => await ExtractLinkCandidatesAsync();

    private static bool IsNavigationalNoise(string text, string href, string cssClass)
    {
        var noiseTexts = new[]
        {
            "home", "about", "contact", "privacy", "terms",
            "cookie", "sitemap", "search", "menu", "skip to"
        };
        var noiseClasses = new[]
        {
            "footer", "breadcrumb", "social", "cookie",
            "header", "logo", "menu", "skip"
        };
        var noiseHrefs = new[]
        {
            "#", "javascript:", "mailto:", "tel:",
            "/contact", "/about", "/privacy", "/terms"
        };

        var textLower = text.ToLower();
        var classLower = cssClass.ToLower();
        var hrefLower = href.ToLower();

        return noiseTexts.Any(n => textLower == n) ||
               noiseClasses.Any(n => classLower.Contains(n)) ||
               noiseHrefs.Any(n => hrefLower.StartsWith(n));
    }

    private static bool IsContentNoise(string text, string cssClass)
    {
        var noiseClasses = new[]
            { "nav", "footer", "header", "cookie", "menu", "breadcrumb" };
        var noiseTexts = new[]
            { "cookie", "privacy", "terms", "copyright", "all rights reserved" };

        var classLower = cssClass.ToLower();
        var textLower = text.ToLower();

        return noiseClasses.Any(n => classLower.Contains(n)) ||
               noiseTexts.Any(n => textLower.Contains(n));
    }

    private static async Task<string> BuildSelectorAsync(
        IElementHandle element, string href, string cssClass)
    {
        if (!string.IsNullOrWhiteSpace(href))
            return $"a[href='{href}']";

        if (!string.IsNullOrWhiteSpace(cssClass))
            return $".{cssClass.Split(' ')[0]}";

        return await element.EvaluateAsync<string>("""
            el => {
                if (el.id) return '#' + el.id;
                const tag = el.tagName.toLowerCase();
                const parent = el.parentElement;
                if (parent) {
                    const siblings = [...parent.children].filter(c => c.tagName === el.tagName);
                    if (siblings.length > 1) {
                        const idx = siblings.indexOf(el) + 1;
                        return tag + ':nth-child(' + idx + ')';
                    }
                }
                return tag;
            }
        """);
    }
}