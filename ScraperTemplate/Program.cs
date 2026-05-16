using Microsoft.Playwright;
using ScraperTemplate.Models;
using System.Collections.Generic;
using ScraperTemplate.Export;

namespace ScraperTemplate;

class Program {
    static async Task Main(string[] args) {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync( new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        
        try
        {
            /*
             * Change the link in the following line of code to the website that needs to be scraped.
             */
            await page.GotoAsync("http://quotes.toscrape.com/login"); // navigate to login page first

            var scraper = new Scraper.Scraper(page);

            // Login — skip this block entirely if the site has no login wall
            await scraper.LoginAsync(
                username: "your@email.com",
                password: "yourpassword"
            );

            Console.WriteLine($"Landed on: {page.Url}");
            // Now navigate to the actual content page after login
            // await page.GotoAsync("http://quotes.toscrape.com");
            // Scrape guidelines
            var guidelines = await scraper.ScrapeGuidelines();
            CsvExporter.Export(guidelines, "guidelines.csv");

            // Scrape documents
            var documents = await scraper.ScrapeGuidelineDocuments();
            CsvExporter.Export(documents, "documents.csv");
        }
        finally
        {
            await browser.CloseAsync();
        }
    }
}