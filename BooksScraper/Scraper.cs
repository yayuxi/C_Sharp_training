using BooksScraper.Models;
using Microsoft.Playwright;

namespace BooksScraper;

public class Scraper
{
    private readonly IPage _page;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10);
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 3000;

    public Scraper(IPage page)
    {
        _page = page;
    }

    public async Task<List<Book>> ScrapeAllAsync()
    {
        var books = new List<Book>();
        int pageNumber = 1;

        while (true)
        {
            var bookElements = await _page.QuerySelectorAllAsync(".product_pod");

            // Project each element into a task and run them all concurrently
            var tasks = bookElements.Select(async bookElement =>
            {
                try
                {
                    return await ScrapeBookAsync(bookElement);
                }
                catch (ScraperException ex)
                {
                    Console.WriteLine($"[Warning] Skipping book on page {pageNumber} — {ex.Message}");
                    return null;
                }
            });

            // Wait for all 20 book tasks on this page to finish
            var results = await Task.WhenAll(tasks);

            // Filter out any nulls from skipped books
            var pageBooks = results.Where(b => b != null).ToList();
            books.AddRange(pageBooks);

            int failCount = results.Count(b => b == null);
            Console.WriteLine($"[Page {pageNumber}] Scraped {pageBooks.Count} books" +
                              (failCount > 0 ? $", skipped {failCount}" : "") +
                              $" — Total so far: {books.Count}");

            var nextButton = await _page.QuerySelectorAsync(".next");
            if (nextButton == null) break;

            var nextUrl = await GetNextPageUrlAsync();
            await NavigateWithRetryAsync(nextUrl);
            pageNumber++;
        }

        return books;
    }

    private async Task<Book> ScrapeBookAsync(IElementHandle bookElement)
    {
        var titleElement = await bookElement.QuerySelectorAsync("h3 a")
            ?? throw new ElementNotFoundException("h3 a (title)");
        var priceElement = await bookElement.QuerySelectorAsync(".price_color")
            ?? throw new ElementNotFoundException(".price_color");
        var availabilityElement = await bookElement.QuerySelectorAsync(".availability")
            ?? throw new ElementNotFoundException(".availability");
        var starElement = await bookElement.QuerySelectorAsync(".star-rating")
            ?? throw new ElementNotFoundException(".star-rating");

        var titleText = await titleElement.GetAttributeAsync("title")
            ?? throw new ElementNotFoundException("title attribute");
        var priceText = (await priceElement.InnerTextAsync()).Replace("£", "").Trim();
        var availabilityText = (await availabilityElement.InnerTextAsync()).Trim();
        var starClass = await starElement.GetAttributeAsync("class") ?? "";

        // Get the book detail URL and navigate to it for category
        var relativeUrl = await titleElement.GetAttributeAsync("href")
            ?? throw new ElementNotFoundException("href attribute on title");
        var bookUrl = BuildBookUrl(relativeUrl);
        var category = await ScrapeBookCategoryAsync(bookUrl);

        if (!double.TryParse(priceText, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price))
            throw new ScraperException($"Could not parse price: '{priceText}'");

        return new Book
        {
            Title = titleText,
            Price = price,
            Availability = availabilityText == "In stock",
            StarRating = ParseStarRating(starClass),
            Category = category
        };
    }

    private async Task<string> ScrapeBookCategoryAsync(string bookUrl)
    {
        await _semaphore.WaitAsync(); // wait for a free slot
        var detailPage = await _page.Context.NewPageAsync();
        try
        {
            await NavigateWithRetryAsync(bookUrl, detailPage);
            var breadcrumbs = await detailPage.QuerySelectorAllAsync(".breadcrumb li");
            if (breadcrumbs.Count < 3)
                throw new ElementNotFoundException("category breadcrumb");
            return (await breadcrumbs[^2].InnerTextAsync()).Trim();
        }
        finally
        {
            await detailPage.CloseAsync();
            _semaphore.Release(); // always release the slot, even if an exception occurred
        }
    }

    private async Task NavigateWithRetryAsync(string url, IPage? targetPage = null)
    {
        var pg = targetPage ?? _page;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await pg.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle, // Changed from DOMContentLoaded
                    Timeout = 3000
                });
                return; // If GotoAsync succeeds with NetworkIdle, page is fully ready
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Console.WriteLine($"[Retry {attempt}/{MaxRetries}] Failed to load {url} — {ex.Message}");
                await Task.Delay(RetryDelayMs);
            }
        }

        throw new PageLoadException(url);
    }

    private async Task<string> GetNextPageUrlAsync()
    {
        var nextLink = await _page.QuerySelectorAsync(".next a")
            ?? throw new ElementNotFoundException(".next a");
        var href = await nextLink.GetAttributeAsync("href")
            ?? throw new ElementNotFoundException("href on next button");

        var currentUrl = _page.Url;
        var basePath = currentUrl[..currentUrl.LastIndexOf('/')];
        return $"{basePath}/{href}";
    }

    private static string BuildBookUrl(string relativeUrl)
    {
        var cleaned = relativeUrl.TrimStart('.', '/').TrimStart('/');
    
        // If it already contains "catalogue", don't add it again
        if (cleaned.StartsWith("catalogue/"))
            return $"https://books.toscrape.com/{cleaned}";
    
        return $"https://books.toscrape.com/catalogue/{cleaned}";
    }

    private static int ParseStarRating(string cssClass) => cssClass switch
    {
        var c when c.Contains("One")   => 1,
        var c when c.Contains("Two")   => 2,
        var c when c.Contains("Three") => 3,
        var c when c.Contains("Four")  => 4,
        var c when c.Contains("Five")  => 5,
        _ => 0
    };
}