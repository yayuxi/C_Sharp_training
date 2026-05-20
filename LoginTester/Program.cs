using Microsoft.Playwright;
using System.Text.RegularExpressions;
using LoginTester;

var sites = new[]
{
    new LoginTarget
    {
        Name = "Quotes to Scrape",
        LoginUrl = "http://quotes.toscrape.com/login",
        Username = "user",
        Password = "password",
        SuccessIndicator = "[href*='logout']"
    },
    new LoginTarget
    {
        Name = "Microsoft",
        LoginUrl = "https://login.microsoftonline.com",
        Username = "s235109@dtu.dk",
        Password = "ra2KJ9eNfPM6",
        SuccessIndicator = "[aria-label='account manager']"
    },
    new LoginTarget
    {
        Name = "Google",
        LoginUrl = "https://accounts.google.com",
        Username = "eliasyanmortensen@gmail.com",
        Password = "Xixixi223-",
        SuccessIndicator = "[aria-label*='Google Account']"
    }
};

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(
    new BrowserTypeLaunchOptions { Headless = false });

foreach (var site in sites)
{
    Console.WriteLine($"\n{'='.Repeat(50)}");
    Console.WriteLine($"Testing: {site.Name}");
    Console.WriteLine($"{'='.Repeat(50)}");

    var context = await browser.NewContextAsync(new BrowserNewContextOptions
    {
        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/124.0.0.0 Safari/537.36",
        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
    });
    var page = await context.NewPageAsync();

    var tester = new LoginTester.LoginTester(page);
    var result = await tester.TestLoginAsync(site);

    Console.WriteLine(result.Success
        ? $"[PASS] {site.Name} — logged in successfully"
        : $"[FAIL] {site.Name} — {result.FailureReason}");

    // Screenshot final state regardless of outcome
    await page.ScreenshotAsync(new PageScreenshotOptions
    {
        Path = $"result_{site.Name.Replace(" ", "_")}.png",
        FullPage = true
    });

    await context.CloseAsync();
    await Task.Delay(2000); // pause between sites
}

Console.WriteLine("\nAll tests complete.");

public static class StringExtensions
{
    public static string Repeat(this char c, int count) => new string(c, count);
}