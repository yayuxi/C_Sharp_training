using Microsoft.Playwright;
using ScraperTemplate.Export;
using ScraperTemplate.Helpers;
using ScraperTemplate.Scraper;

namespace ScraperTemplate;

class Program
{
    static async Task Main(string[] args)
    {
        // ╔══════════════════════════════════════════════════════════════╗
        // ║                    CONFIGURATION                            ║
        // ║  These are the only values you need to change per scraper   ║
        // ╚══════════════════════════════════════════════════════════════╝

        // ↓ URL of the page to scrape (or login page if login is required)
        const string targetUrl = "https://quotes.toscrape.com/login";

        // ↓ Set to true if the site requires login, false to skip
        const bool requiresLogin = true;

        // ↓ Credentials — only used if requiresLogin is true
        const string username = "user";
        const string password = "password";

        // ↓ Set to true to use the autonomous AI-powered scraper
        const bool useAutoScraper = true;

        // ↓ Hugging Face API key — only needed if useAutoScraper is true
        const AiProvider aiProvider = AiProvider.Ollama;
        const string aiApiKey = "";

        // ↓ Describe what you're looking for — the AI uses this as its goal
        const string scrapingGoal = "quotes and author names";

        // ╔══════════════════════════════════════════════════════════════╗
        // ║              NO CHANGES NEEDED BELOW THIS LINE              ║
        // ╚══════════════════════════════════════════════════════════════╝

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        try
        {
            // Step 1: handle login if required
            if (requiresLogin)
            {
                await page.GotoAsync(targetUrl, new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle });

                var scraper = new Scraper.Scraper(page);
                await scraper.LoginAsync(username, password);

                // After login the site redirects — navigate to the actual content page
                // ↓ Change this to the content URL if different from the login URL
                await page.GotoAsync("https://quotes.toscrape.com", new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle });
            }
            else
            {
                await page.GotoAsync(targetUrl, new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle });
            }

            // Step 2: scrape using either auto or manual scraper
            if (useAutoScraper)
            {
                var autoScraper = new AutoScraper(page, aiApiKey, aiProvider);

                var (guidelines, documents) = await autoScraper.ScrapeAsync(
                    url: "https://quotes.toscrape.com",
                    guidelineGoal: "quotes or text content — links containing quote text or author names",
                    documentGoal: "links to other pages, tags, or author pages");

                CsvExporter.Export(guidelines, "auto_guidelines.csv");
                CsvExporter.Export(documents, "auto_documents.csv");

                Console.WriteLine($"[Done] {guidelines.Count} guidelines, " +
                                  $"{documents.Count} documents saved");
            }
            else
            {
                var scraper = new Scraper.Scraper(page);
                var guidelines = await scraper.ScrapeGuidelines();
                CsvExporter.Export(guidelines, "guidelines.csv");

                var documents = await scraper.ScrapeGuidelineDocuments();
                CsvExporter.Export(documents, "documents.csv");

                Console.WriteLine($"[Done] Saved {guidelines.Count} guidelines " +
                                  $"and {documents.Count} documents");
            }
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