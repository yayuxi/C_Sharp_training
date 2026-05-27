using Microsoft.Playwright;
using ScraperTemplate.Export;
using ScraperTemplate.Scraper;

namespace ScraperTemplate;

class Program
{
    static async Task Main(string[] args)
    {
        // ╔══════════════════════════════════════════════════════════════╗
        // ║                    CONFIGURATION                             ║
        // ║  These are the only values you need to change per scraper    ║
        // ╚══════════════════════════════════════════════════════════════╝

        // ↓ URL of the page to scrape (or login page if login is required)
        const string targetUrl = "https://quotes.toscrape.com/login";

        // ↓ Set to true if the site requires login, false to skip
        const bool requiresLogin = true;

        // ↓ Credentials — only used if requiresLogin is true
        const string username = "your@email.com";
        const string password = "yourpassword";
        
        // ↓ Anti-bot protection tier to apply from the start:
        //   Tier 1 — No protection       (public sites, no bot detection)
        //   Tier 2 — Human-like behavior (basic bot detection, rate limiting)
        //   Tier 3 — Full stealth        (fingerprinting, strong bot detection)
        const int antiBotTier = 1; 

        // ╔══════════════════════════════════════════════════════════════╗
        // ║              NO CHANGES NEEDED BELOW THIS LINE               ║
        // ╚══════════════════════════════════════════════════════════════╝

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        var tester = new AntiBotTester(page);
        await tester.RunAllTestsAsync();

        try
        {
            var scraper = new Scraper.Scraper(page, antiBotTier);

            if (requiresLogin)
            {
                await page.GotoAsync(targetUrl, new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle });
                await scraper.LoginAsync(username, password);
            }
            else
            {
                await page.GotoAsync(targetUrl, new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle });
            }

            var guidelines = await scraper.ScrapeGuidelines();
            CsvExporter.Export(guidelines, "guidelines.csv");

            var documents = await scraper.ScrapeGuidelineDocuments();
            CsvExporter.Export(documents, "documents.csv");
        }
        catch (ScraperException ex)
        {
            Console.WriteLine($"[Fatal] {ex.Message}");
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}