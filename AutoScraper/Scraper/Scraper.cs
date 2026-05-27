using Microsoft.Playwright;
using ScraperTemplate.Helpers;
using ScraperTemplate.Models;

namespace ScraperTemplate.Scraper;

/// <summary>
/// ╔══════════════════════════════════════════════════════════════╗
/// ║                    SCRAPER TEMPLATE                          ║
/// ║                                                              ║
/// ║  Two files need editing when building a new scraper:         ║
/// ║  1. Program.cs  — set URL, login credentials                 ║
/// ║  2. Scraper.cs  — implement ScrapeGuidelines()               ║
/// ║                   and ScrapeGuidelineDocuments()             ║
/// ║                                                              ║
/// ║  Optionally override IsLoggedInAsync() if the default        ║
/// ║  URL-based check doesn't work for the target site.           ║
/// ╚══════════════════════════════════════════════════════════════╝
/// </summary>
public class Scraper
{
    private readonly IPage _page;
    private readonly LoginHandler _loginHandler;

    public Scraper(IPage page)
    {
        _page = page;
        _loginHandler = new LoginHandler(page);
    }

    // -------------------------------------------------------------------------
    // Login — no changes needed here for most sites
    // -------------------------------------------------------------------------

    /// <summary>
    /// Logs in using the provided credentials.
    /// LoginHandler handles form detection, multi-step flows, and session persistence.
    /// </summary>
    public async Task LoginAsync(string username, string password)
        => await _loginHandler.LoginAsync(username, password, IsLoggedInAsync);

    /// <summary>
    /// Override this if the target site doesn't change its URL after login.
    /// Example: check for a user avatar or logout button instead.
    /// </summary>
    protected virtual async Task<bool> IsLoggedInAsync()
    {
        var url = _page.Url.ToLower();
        var isOnLoginPage = new[] { "login", "signin", "sign-in", "auth" }
            .Any(url.Contains);

        var postLoginElement = await _page.QuerySelectorAsync(
            "[class*='dashboard'], [class*='profile'], [class*='account'], " +
            "[href*='logout'], [href*='sign-out']");

        return !isOnLoginPage || postLoginElement != null;
    }

    // -------------------------------------------------------------------------
    // ↓↓↓ EDIT BELOW THIS LINE FOR EACH NEW SCRAPER ↓↓↓
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scrapes the main list of guidelines from the target site.
    /// Replace the selector logic below with the structure of the target site.
    /// </summary>
    public async Task<List<Guideline>> ScrapeGuidelines()
    {
        // Wait for the main content container to appear
        // ↓ Change ".product_pod" to the main content selector on the target site
        // await RetryHelper.ExecuteAsync(
        //     () => _page.WaitForSelectorAsync(".product_pod"),
        //     "Waiting for content to load");

        // ↓ Change ".quote" to the selector for each guideline item
        var elements = await _page.QuerySelectorAllAsync(".quote");
        var guidelines = new List<Guideline>();

        foreach (var element in elements)
        {
            // ↓ Replace these with the actual fields from the target site
            var title = await element.InnerTextAsync();
            var url = await element.GetAttributeAsync("href");
            var tags = await element.QuerySelectorAllAsync(".tag");
            var tagsList = new List<string>();
            foreach (var tag in tags)                                   
                tagsList.Add(await tag.InnerTextAsync());

            guidelines.Add(new Guideline
            {
                GuidelineCode = Guid.NewGuid().ToString(), // ↓ replace with real code
                Title = title.Trim(),
                Category = string.Join("|", tagsList),     // ↓ replace with real category
                Step = "1",                                // ↓ replace with real step
                Status = "Active",                         // ↓ replace with real status
                Dated = DateTime.Now,                      // ↓ replace with real date
                Summary = $"Template guideline: {title}",  // ↓ replace with real summary
                SourceUrl = url ?? ""
            });
        }

        return guidelines;
    }

    /// <summary>
    /// Scrapes document links associated with each guideline.
    /// Replace the selector logic below with the structure of the target site.
    /// </summary>
    public async Task<List<GuidelineDocument>> ScrapeGuidelineDocuments()
    {
        // ↓ Change ".product_pod img" to the selector for document elements
        // await RetryHelper.ExecuteAsync(
        //     () => _page.WaitForSelectorAsync(".product_pod img"),
        //     "Waiting for document elements to load");

        var elements = await _page.QuerySelectorAllAsync(".quote");
        var documents = new List<GuidelineDocument>();

        foreach (var element in elements)
        {
            // ↓ Replace these with the actual fields from the target site
            var title = await element.GetAttributeAsync("alt");
            var url = await element.GetAttributeAsync("src");

            documents.Add(new GuidelineDocument
            {
                GuidelineCode = Guid.NewGuid().ToString(), // ↓ replace with real code
                DocumentTitle = title ?? "Untitled",
                DocumentUrl = (url != null && url.StartsWith("http"))
                    ? url
                    : $"https://quotes.toscrape.com{url}",  // ↓ replace base URL
                DocumentType = "Image",                    // ↓ replace with real type
                FileFormat = "jpg"                         // ↓ replace with real format
            });
        }

        return documents;
    }
}