using IchScraper.Export;
using IchScraper.Models;
using IchScraper.Scraper;
using Microsoft.Playwright;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync( new BrowserTypeLaunchOptions { Headless = false });
var context = await browser.NewContextAsync();
var page = await context.NewPageAsync();

var allGuidelines = new List<Guideline>();
var allDocuments = new List<GuidelineDocument>();

try
{
    // Step 1: get the 4 category links from the index page
    var indexScraper = new IchIndexScraper(page);
    var categories = await indexScraper.GetCategoriesAsync();

    // Step 2: scrape each category page
    var guidelineScraper = new IchGuidelineScraper(page);
    foreach (var (name, url) in categories)
    {
        Console.WriteLine($"\n=== Scraping category: {name} ===");
        var (guidelines, documents) = await guidelineScraper.ScrapeCategoryAsync(name, url);
        allGuidelines.AddRange(guidelines);
        allDocuments.AddRange(documents);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Fatal] {ex.Message}");
}
finally
{
    CsvExporter.ExportGuidelines(allGuidelines, "ich_guidelines.csv");
    CsvExporter.ExportDocuments(allDocuments, "ich_documents.csv");
    await browser.CloseAsync();
}