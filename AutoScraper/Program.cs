using Microsoft.Playwright;
using ScraperTemplate.Export;
using ScraperTemplate.Helpers;
using ScraperTemplate.Models;
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

        // ↓ URL of the index page containing category links
        const string targetUrl = "https://www.ich.org/page/ich-guidelines";

        // ↓ Set to true if the site requires login, false to skip
        const bool requiresLogin = false;

        // ↓ Credentials — only used if requiresLogin is true
        const string username = "";
        const string password = "";

        // ↓ Set to true to use the autonomous AI-powered scraper
        const bool useAutoScraper = true;

        // ↓ AI provider and key
        const AiProvider aiProvider = AiProvider.Ollama;
        const string aiApiKey = "";

        // ↓ Goals describing what to find on each category page
        const string guidelineGoal =
            "guideline codes like Q1 Q2 E1 S1, guideline titles, " +
            "or regulatory document names in pharmaceutical guidelines";
        const string documentGoal =
            "PDF files, downloadable guideline documents, " +
            "or links with class document-pdf or document-link";

        // ╔══════════════════════════════════════════════════════════════╗
        // ║              NO CHANGES NEEDED BELOW THIS LINE              ║
        // ╚══════════════════════════════════════════════════════════════╝

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var allGuidelines = new List<Guideline>();
        var allDocuments = new List<GuidelineDocument>();

        try
        {
            // Step 1: handle login if required
            if (requiresLogin)
            {
                await page.GotoAsync(targetUrl, new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle });
                var scraper = new Scraper.Scraper(page);
                await scraper.LoginAsync(username, password);
            }

            if (!useAutoScraper)
            {
                // Manual scraper path
                await page.GotoAsync(targetUrl, new PageGotoOptions
                    { WaitUntil = WaitUntilState.NetworkIdle });
                var scraper = new Scraper.Scraper(page);
                var guidelines = await scraper.ScrapeGuidelines();
                CsvExporter.Export(guidelines, "guidelines.csv");
                var documents = await scraper.ScrapeGuidelineDocuments();
                CsvExporter.Export(documents, "documents.csv");
                Console.WriteLine($"[Done] Saved {guidelines.Count} guidelines " +
                                  $"and {documents.Count} documents");
                return;
            }

            // Step 2: discover category URLs from the index page
            Console.WriteLine("[Discovery] Loading ICH index page...");
            await page.GotoAsync(targetUrl, new PageGotoOptions
                { WaitUntil = WaitUntilState.NetworkIdle });

            // Wait for Angular app to render the category links
            await page.WaitForSelectorAsync("app-guidelines-diagram",
                new PageWaitForSelectorOptions { Timeout = 15000 });
            await page.WaitForTimeoutAsync(2000);

            var categoryLinks = await page.QuerySelectorAllAsync(
                "app-guidelines-diagram section > div app-guideline-item h3 a");

            var categoryUrls = new List<(string Name, string Url)>();
            foreach (var link in categoryLinks)
            {
                var name = (await link.InnerTextAsync()).Trim();
                var href = await link.GetAttributeAsync("href") ?? "";
                var url = href.StartsWith("http") ? href : $"https://www.ich.org{href}";
                categoryUrls.Add((name, url));
                Console.WriteLine($"[Discovery] Found category: {name} → {url}");
            }

            if (categoryUrls.Count == 0)
            {
                Console.WriteLine("[Discovery] No categories found — " +
                                  "check that the Angular app has rendered correctly");
                return;
            }

            // Step 3: set up autoscraper with ICH-specific accordion expansion
            var autoScraper = new AutoScraper(page, aiApiKey, aiProvider);

            autoScraper.PreScrapeAction = async (p) =>
            {
                await p.WaitForSelectorAsync("jaspero-accordion",
                    new PageWaitForSelectorOptions { Timeout = 15000 });
                await p.WaitForTimeoutAsync(2000);

                // Step 1: expand outer accordions one by one and wait for inner content
                var outerAccords = await p.QuerySelectorAllAsync(
                    "app-accordion > jaspero-accordion > jaspero-accord");
                Console.WriteLine($"[PreScrape] Found {outerAccords.Count} outer accordions");

                foreach (var outerAccord in outerAccords)
                {
                    try
                    {
                        // Click the header div of this specific outer accordion
                        var outerHeader = await outerAccord.QuerySelectorAsync("div:first-child");
                        if (outerHeader == null) continue;

                        await outerHeader.ClickAsync();

                        // Wait for inner jaspero-accordion to appear inside this outer accord
                        await p.WaitForTimeoutAsync(600);

                        // Step 2: expand all inner accordions within this outer accord
                        var innerAccords = await outerAccord.QuerySelectorAllAsync(
                            "jaspero-accordion > jaspero-accord");
                        Console.WriteLine($"[PreScrape] Expanding {innerAccords.Count} " +
                                          $"inner accordions in group...");

                        foreach (var innerAccord in innerAccords)
                        {
                            try
                            {
                                var innerHeader = await innerAccord.QuerySelectorAsync("div:first-child");
                                if (innerHeader == null) continue;

                                await innerHeader.ClickAsync();
                                await p.WaitForTimeoutAsync(300);
                            }
                            catch { /* skip unclickable inner headers */ }
                        }
                    }
                    catch { /* skip unclickable outer headers */ }
                }

                // Step 3: wait for all content to fully render after expansion
                await p.WaitForTimeoutAsync(1000);
                Console.WriteLine("[PreScrape] All accordions expanded");
            };

            // Step 4: scrape each category page
            foreach (var (categoryName, categoryUrl) in categoryUrls)
            {
                Console.WriteLine($"\n{new string('=', 50)}");
                Console.WriteLine($"Scraping category: {categoryName}");
                Console.WriteLine(new string('=', 50));

                // Clear cache between categories so selectors don't cross-contaminate
                File.Delete("selector_cache.json");

                var (guidelines, documents) = await autoScraper.ScrapeAsync(
                    url: categoryUrl,
                    guidelineGoal: guidelineGoal,
                    documentGoal: documentGoal);

                // Tag each guideline with its category name
                foreach (var g in guidelines)
                    allGuidelines.Add(g with { Category = categoryName });

                allDocuments.AddRange(documents);

                Console.WriteLine($"[Category done] {guidelines.Count} guidelines, " +
                                  $"{documents.Count} documents from {categoryName}");
            }
        }
        catch (ScraperException ex)
        {
            Console.WriteLine($"[Fatal] {ex.Message}");
        }
        finally
        {
            // Always save whatever was collected, even if an error occurred mid-run
            CsvExporter.Export(allGuidelines, "auto_guidelines.csv");
            CsvExporter.Export(allDocuments, "auto_documents.csv");
            Console.WriteLine($"\n[Done] {allGuidelines.Count} total guidelines, " +
                              $"{allDocuments.Count} total documents saved");

            await browser.CloseAsync();
        }
    }
}