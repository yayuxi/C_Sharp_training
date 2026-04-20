using BooksScraper.Export;
using Microsoft.Playwright;

namespace BooksScraper;

public class Program
{
    public static async Task Main(string[] args)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync("https://books.toscrape.com");

            var scraper = new Scraper(page);
            var books = await scraper.ScrapeAllAsync();

            CsvExporter.Export(books, "books_complete.csv");
        }
        catch (PageLoadException ex)
        {
            Console.WriteLine($"[Fatal] {ex.Message}");
        }
        catch (ScraperException ex)
        {
            Console.WriteLine($"[Fatal] Scraper error: {ex.Message}");
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}