using Microsoft.Playwright;
using ScraberTemplate.Export;
using ScraberTemplate.Models;
using System.Collections.Generic;

namespace ScraberTemplate;

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
            await page.GotoAsync("https://books.toscrape.com");
            
            

            var scraper = new Scraper.Scraper(page);

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