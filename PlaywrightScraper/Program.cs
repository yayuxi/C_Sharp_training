using System.Threading.Tasks;
using Microsoft.Playwright;
using CsvHelper;
using System.Globalization;

class Program
{
    // Week 5
    // // Initialize Playwright
    // using var playwright = await Playwright.CreateAsync();
    //
    // // Launch a browser
    // await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
    //
    // // Create a browser context and a page
    // var context = await browser.NewContextAsync();
    // var page = await context.NewPageAsync();
    //
    // // Navigate to a website
    // await page.GotoAsync("https://playwright.dev");
    //     
    // // Get and print the page title
    // string title = await page.TitleAsync();
    // Console.WriteLine($"Page title: {title}");
    //
    // // Take a screenshot
    // await page.ScreenshotAsync(new PageScreenshotOptions { Path = "screenshot.png" });
    //
    // // Close the browser
    // await browser.CloseAsync();
    
    // using var playwright = await Playwright.CreateAsync();
    // await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
    // var context = await browser.NewContextAsync();
    // var page = await context.NewPageAsync();
    // await page.GotoAsync("https://books.toscrape.com");
    // List<Dictionary<string, string>> books = new List<Dictionary<string, string>>();
    // var bookElements = await page.QuerySelectorAllAsync(".product_pod");
    // foreach (var bookElement in bookElements) {
    //     var title = await bookElement.QuerySelectorAsync("h3 a");
    //     var price = await bookElement.QuerySelectorAsync(".price_color");
    //     var titleText = await title.GetAttributeAsync("title");
    //     var priceText = await price.InnerTextAsync();
    //     books.Add(new Dictionary<string, string> {
    //         { "title", titleText },
    //         { "price", priceText }
    //     });
    // }
    // System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions {
    //     WriteIndented = true
    // };
    // string json = System.Text.Json.JsonSerializer.Serialize(books, options);
    // System.IO.File.WriteAllText("books.json", json);
    //     
    // await browser.CloseAsync();
    public static async Task Main(string[] args)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync("https://quotes.toscrape.com");
        List<Quote> quotes = new List<Quote>();

        while (true)
        {
            var bookElements = await page.QuerySelectorAllAsync(".quote"); 

            foreach (var bookElement in bookElements)
            {
                var quote = await bookElement.QuerySelectorAsync(".text");   
                var author = await bookElement.QuerySelectorAsync(".author");
                var tags = await bookElement.QuerySelectorAllAsync(".tag");

                var quoteText = await quote.InnerTextAsync();               
                var authorText = await author.InnerTextAsync();
                var tagsList = new List<string>();
                foreach (var tag in tags)                                   
                    tagsList.Add(await tag.InnerTextAsync());

                quotes.Add(new Quote {
                    Text = quoteText,
                    Author = authorText,
                    Tags = string.Join("|", tagsList) 
                });
            }

            var nextButton = await page.QuerySelectorAsync(".next");       
            if (nextButton == null)
                break;

            await page.ClickAsync(".next a");
            await page.WaitForSelectorAsync(".quote");                     
        }

        Console.WriteLine($"Scraped {quotes.Count} quotes!");

        using var writer = new StreamWriter("quotes.csv");
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(quotes);

        Console.WriteLine($"Saved {quotes.Count} quotes to quotes.csv!");

        await browser.CloseAsync();
    }
    
    public class Quote
    {
        public string Text { get; set; }
        public string Author { get; set; }
        public string Tags { get; set; } 
    }
}