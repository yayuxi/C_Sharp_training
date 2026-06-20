using Microsoft.Playwright;

namespace ScraperTemplate.Helpers;

/// <summary>
/// Extracts interactive elements from a Playwright page into lightweight
/// summaries suitable for sending to an AI model.
/// </summary>
public class PageElementExtractor
{
    private readonly IPage _page;

    public PageElementExtractor(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Extracts all links and buttons from the page with their surrounding context.
    /// Filters out obvious navigation/footer noise to reduce token count.
    /// </summary>
    public async Task<List<ElementSummary>> ExtractCandidatesAsync()
    {
        var elements = await _page.QuerySelectorAllAsync("a[href], button");
        var summaries = new List<ElementSummary>();
        int index = 1;

        foreach (var element in elements)
        {
            try
            {
                // Skip invisible elements
                if (!await element.IsVisibleAsync()) continue;

                var text = (await element.InnerTextAsync()).Trim();
                var href = await element.GetAttributeAsync("href") ?? "";
                var cssClass = await element.GetAttributeAsync("class") ?? "";
                var tagName = await element.EvaluateAsync<string>("el => el.tagName.toLowerCase()");

                // Skip empty or purely navigational links
                if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(href))
                    continue;
                if (IsNavigationalNoise(text, href, cssClass))
                    continue;

                // Get parent context for richer AI understanding
                var parentClass = await element.EvaluateAsync<string>(
                    "el => el.parentElement?.className ?? ''");
                var parentText = (await element.EvaluateAsync<string>(
                    "el => el.parentElement?.innerText ?? ''"))
                    .Split('\n')[0] // first line only
                    .Trim();

                // Build a unique CSS selector for this element
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
            catch
            {
                // Skip elements that throw during extraction
            }
        }

        return summaries;
    }

    /// <summary>
    /// Filters out links that are obviously not documents —
    /// nav bars, footers, social media, etc.
    /// </summary>
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

    private static async Task<string> BuildSelectorAsync(
        IElementHandle element, string href, string cssClass)
    {
        // Build the most specific selector we can from available attributes
        if (!string.IsNullOrWhiteSpace(href))
            return $"a[href='{href}']";

        if (!string.IsNullOrWhiteSpace(cssClass))
        {
            var firstClass = cssClass.Split(' ')[0];
            return $".{firstClass}";
        }

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