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

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var allGuidelines = new List<Guideline>();
        var allDocuments = new List<GuidelineDocument>();

        // ---------------------------------------------------------------
        // Pre-scrape actions defined once and reused across all categories
        // ---------------------------------------------------------------

        // Phase 1: open just ONE outer and ONE inner accordion for AI analysis
        // Keeps the HTML sample small and focused so the local model can read it accurately
        Func<IPage, Task> limitedPreScrapeAction = async (p) =>
        {
            await p.WaitForSelectorAsync("jaspero-accordion",
                new PageWaitForSelectorOptions { Timeout = 15000 });
            await p.WaitForTimeoutAsync(2000);

            var outerAccords = await p.QuerySelectorAllAsync(
                "app-accordion > jaspero-accordion > jaspero-accord");
            if (outerAccords.Count == 0) return;

            // Open only the first outer accordion
            var firstOuter = outerAccords[0];
            var firstOuterHeader = await firstOuter.QuerySelectorAsync("div:first-child");
            if (firstOuterHeader != null)
            {
                await firstOuterHeader.ClickAsync();
                await p.WaitForTimeoutAsync(600);
            }

            // Open only the first inner accordion inside it
            var innerAccords = await firstOuter.QuerySelectorAllAsync(
                "jaspero-accordion > jaspero-accord");
            if (innerAccords.Count > 0)
            {
                var firstInnerHeader = await innerAccords[0].QuerySelectorAsync("div:first-child");
                if (firstInnerHeader != null)
                {
                    await firstInnerHeader.ClickAsync();
                    await p.WaitForTimeoutAsync(400);
                }
            }

            Console.WriteLine("[PreScrape] Limited expansion — 1 outer, 1 inner open for AI analysis");
        };

        // Phase 2: expand ALL accordions for full scraping
        Func<IPage, Task> fullPreScrapeAction = async (p) =>
        {
            var outerAccords = await p.QuerySelectorAllAsync(
                "app-accordion > jaspero-accordion > jaspero-accord");
            Console.WriteLine($"[PreScrape] Found {outerAccords.Count} outer accordions");

            foreach (var outerAccord in outerAccords)
            {
                try
                {
                    // Check if already open by looking for visible inner content
                    var alreadyOpen = await outerAccord.QuerySelectorAsync(
                        "jaspero-accordion > jaspero-accord") != null;

                    if (!alreadyOpen)
                    {
                        var outerHeader = await outerAccord.QuerySelectorAsync("div:first-child");
                        if (outerHeader == null) continue;
                        await outerHeader.ClickAsync();
                        await p.WaitForTimeoutAsync(600);
                    }

                    var innerAccords = await outerAccord.QuerySelectorAllAsync(
                        "jaspero-accordion > jaspero-accord");
                    Console.WriteLine(
                        $"[PreScrape] Expanding {innerAccords.Count} inner accordions in group...");

                    foreach (var innerAccord in innerAccords)
                    {
                        try
                        {
                            // Check if inner is already open
                            var innerContent = await innerAccord.QuerySelectorAsync(
                                "app-accordion-guidline");
                            if (innerContent != null) continue;

                            var innerHeader = await innerAccord.QuerySelectorAsync("div:first-child");
                            if (innerHeader == null) continue;
                            await innerHeader.ClickAsync();
                            await p.WaitForTimeoutAsync(300);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            await p.WaitForTimeoutAsync(1000);
            Console.WriteLine("[PreScrape] All accordions expanded");
        };

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

            // Step 3: scrape each category page
            foreach (var (categoryName, categoryUrl) in categoryUrls)
            {
                Console.WriteLine($"\n{new string('=', 50)}");
                Console.WriteLine($"Scraping category: {categoryName}");
                Console.WriteLine(new string('=', 50));

                // Fresh AutoScraper per category — plans reset so AI analyses each page
                var autoScraper = new AutoScraper(page, aiApiKey, aiProvider);
                autoScraper.PreScrapeAction = limitedPreScrapeAction;
                autoScraper.FullExpansionAction = fullPreScrapeAction;

                var (guidelines, documents) = await autoScraper.ScrapeAsync(
                    url: categoryUrl,
                    guidelineGoal: guidelineGoal,
                    documentGoal: documentGoal);

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
            CsvExporter.Export(allGuidelines, "auto_guidelines.csv");
            CsvExporter.Export(allDocuments, "auto_documents.csv");

            var tracker = new RunTracker();
            tracker.RecordRun(allGuidelines.Count, allDocuments.Count);
            tracker.PrintSummary();

            Console.WriteLine($"\n[Done] {allGuidelines.Count} total guidelines, " +
                              $"{allDocuments.Count} total documents saved");

            await browser.CloseAsync();
        }
    }
}