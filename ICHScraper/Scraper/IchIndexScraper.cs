using IchScraper.Helpers;
using Microsoft.Playwright;

namespace IchScraper.Scraper;

public class IchIndexScraper
{
    private readonly IPage _page;
    private const string IndexUrl = "https://www.ich.org/page/ich-guidelines";

    // The 4 category section divs are div[1]..div[4] inside app-guidelines-diagram > section
    private static readonly string[] CategoryDivSelectors = Enumerable
        .Range(1, 4)
        .Select(i => $"app-guidelines-diagram section > div:nth-child({i}) app-guideline-item h3 a")
        .ToArray();

    public IchIndexScraper(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Returns a list of (categoryName, categoryUrl) tuples for all 4 categories.
    /// </summary>
    public async Task<List<(string Name, string Url)>> GetCategoriesAsync()
    {
        await RetryHelper.ExecuteAsync(
            () => _page.GotoAsync(IndexUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }),
            "Loading ICH index page");

        // Wait for the Angular app to render the category links
        await _page.WaitForSelectorAsync("app-guidelines-diagram");

        var categories = new List<(string, string)>();

        foreach (var selector in CategoryDivSelectors)
        {
            var link = await _page.QuerySelectorAsync(selector);
            if (link == null)
            {
                Console.WriteLine($"[Warning] Category link not found for selector: {selector}");
                continue;
            }

            var name = (await link.InnerTextAsync()).Trim();
            var href = await link.GetAttributeAsync("href") ?? "";
            var url = href.StartsWith("http") ? href : $"https://www.ich.org{href}";

            categories.Add((name, url));
            Console.WriteLine($"[Index] Found category: {name} → {url}");
        }

        return categories;
    }
}