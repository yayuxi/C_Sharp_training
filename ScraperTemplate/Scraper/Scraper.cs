using System.Text.Json;
using Microsoft.Playwright;
using ScraperTemplate.Helpers;
using ScraperTemplate.Models;
using System.Text.RegularExpressions;

namespace ScraperTemplate.Scraper;

public class Scraper
{
    private readonly IPage _page;
    private readonly string _sessionFile = "session.json";

    public Scraper(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Attempts to log in to the current page using the provided credentials.
    /// Tries semantic locators first, then falls back to common CSS selectors.
    /// Handles both single-step and multi-step login forms automatically.
    /// Call this after GotoAsync() on the login page.
    /// </summary>
    /// <param name="username">Email or username to log in with</param>
    /// <param name="password">Password to log in with</param>
    public async Task LoginAsync(string username, string password)
    {
        // If a saved session exists, restore it instead of logging in again
        if (File.Exists(_sessionFile))
        {
            Console.WriteLine("[Login] Restoring saved session...");
            await _page.Context.AddCookiesAsync(
                JsonSerializer.Deserialize<List<Cookie>>(
                    await File.ReadAllTextAsync(_sessionFile)) ?? []);

            await _page.ReloadAsync();

            if (await IsLoggedInAsync())
            {
                Console.WriteLine("[Login] Session restored successfully");
                return;
            }

            Console.WriteLine("[Login] Saved session expired, logging in fresh...");
            File.Delete(_sessionFile);
        }

        await RetryHelper.ExecuteAsync(async () =>
        {
            // Fill username/email field
            await FillFieldAsync(
                semanticPatterns: ["email", "username", "user", "login"],
                cssSelectors: [
                    "input[type='email']",
                    "input[name='email']",
                    "input[name='username']",
                    "input[name='user']",
                    "input[name='login']",
                    "input[id*='email']",
                    "input[id*='user']",
                    "input[autocomplete='email']",
                    "input[autocomplete='username']",
                    "input[placeholder*='email' i]",
                    "input[placeholder*='username' i]"
                ],
                value: username,
                fieldName: "username/email");

            // Check if this is a multi-step form (password field not yet visible)
            var isMultiStep = await IsMultiStepFormAsync();
            if (isMultiStep)
            {
                Console.WriteLine("[Login] Multi-step form detected, submitting username first...");
                await ClickSubmitAsync();

                // Wait for the password field to appear on the next step
                await _page.WaitForSelectorAsync("input[type='password']",
                    new PageWaitForSelectorOptions { Timeout = 10000 });
                await HumanBehavior.PauseAsync(500, 1000);
            }

            // Fill password field — type='password' is universal
            await FillFieldAsync(
                semanticPatterns: ["password", "pass"],
                cssSelectors: [
                    "input[type='password']",
                    "input[name='password']",
                    "input[name='pass']",
                    "input[id*='pass']"
                ],
                value: password,
                fieldName: "password");

            await HumanBehavior.PauseAsync(300, 700);
            await ClickSubmitAsync();

            // Wait for navigation after login
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Check if login succeeded
            if (!await IsLoggedInAsync())
                throw new ScraperException("[Login] Login failed — still on login page or credentials rejected");

        }, "[Login] Attempting login");

        // Save session cookies for future runs
        await SaveSessionAsync();
        Console.WriteLine("[Login] Login successful, session saved");
    }

    /// <summary>
    /// Fills an input field using semantic locators first, then CSS selector fallbacks.
    /// </summary>
    private async Task FillFieldAsync(
    string[] semanticPatterns,
    string[] cssSelectors,
    string value,
    string fieldName)
{
    // For password fields, always use type='password' directly — it's universal
    // This prevents accidentally filling the password into a text field
    if (fieldName == "password")
    {
        var passwordInput = await _page.QuerySelectorAsync("input[type='password']");
        if (passwordInput != null)
        {
            await passwordInput.FillAsync(value);
            Console.WriteLine($"[Login] Filled {fieldName} via type='password'");
            return;
        }
    }

    // For username/email, explicitly exclude password fields from all selector matches
    // Layer 1: semantic label matching
    foreach (var pattern in semanticPatterns)
    {
        try
        {
            var locator = _page.GetByLabel(new Regex(pattern, RegexOptions.IgnoreCase));
            if (await locator.CountAsync() > 0)
            {
                // Make sure we didn't accidentally match a password field
                var inputType = await locator.First.GetAttributeAsync("type") ?? "";
                if (inputType == "password") continue;

                await locator.First.FillAsync(value);
                Console.WriteLine($"[Login] Filled {fieldName} via label '{pattern}'");
                return;
            }
        }
        catch { /* try next */ }
    }

    // Layer 2: CSS selector fallbacks — also exclude password type
    foreach (var selector in cssSelectors)
    {
        try
        {
            var element = await _page.QuerySelectorAsync(selector);
            if (element == null) continue;

            var inputType = await element.GetAttributeAsync("type") ?? "";
            if (inputType == "password") continue;

            await element.FillAsync(value);
            Console.WriteLine($"[Login] Filled {fieldName} via selector '{selector}'");
            return;
        }
        catch { /* try next */ }
    }

    throw new ScraperException($"[Login] Could not find {fieldName} field on page");
}

    /// <summary>
    /// Clicks the submit/next button on a login form.
    /// </summary>
    private async Task ClickSubmitAsync()
    {
        var buttonPatterns = new[] { "sign in", "log in", "login", "next", "continue", "submit" };

        var buttons = await _page.QuerySelectorAllAsync("button, input[type='submit']");
        foreach (var button in buttons)
        {
            var text = (await button.InnerTextAsync()).Trim().ToLower();
            if (buttonPatterns.Any(p => text.Contains(p)))
            {
                // Screenshot before clicking so you can inspect the state of the page
                await _page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = $"login_before_submit_{DateTime.Now:HHmmss}.png",
                    FullPage = true
                });
                Console.WriteLine("[Login] Screenshot taken before submit click");

                await button.ClickAsync();
                return;
            }
        }

        var submitInput = await _page.QuerySelectorAsync("input[type='submit']");
        if (submitInput != null)
        {
            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = $"login_before_submit_{DateTime.Now:HHmmss}.png",
                FullPage = true
            });
            Console.WriteLine("[Login] Screenshot taken before submit click");

            await submitInput.ClickAsync();
            return;
        }

        Console.WriteLine("[Login] No submit button found, pressing Enter");
        await _page.Keyboard.PressAsync("Enter");
    }

    /// <summary>
    /// Detects whether the login form hides the password field until username is submitted.
    /// </summary>
    private async Task<bool> IsMultiStepFormAsync()
    {
        var passwordField = await _page.QuerySelectorAsync("input[type='password']");
        if (passwordField == null) return true; // not present at all — definitely multi-step

        // Present but hidden via CSS
        var isVisible = await passwordField.IsVisibleAsync();
        return !isVisible;
    }

    /// <summary>
    /// Override this method to define what "logged in" looks like on the target site.
    /// Default checks that the URL no longer contains login-related keywords.
    /// </summary>
    protected virtual async Task<bool> IsLoggedInAsync()
    {
        var url = _page.Url.ToLower();
        var isOnLoginPage = url.Contains("login") ||
                            url.Contains("signin") ||
                            url.Contains("sign-in") ||
                            url.Contains("auth");

        // Also check for common post-login indicators in the DOM
        var dashboardIndicator = await _page.QuerySelectorAsync(
            "[class*='dashboard'], [class*='profile'], [class*='account'], " +
            "[href*='logout'], [href*='sign-out']");

        return !isOnLoginPage || dashboardIndicator != null;
    }

    /// <summary>
    /// Saves current browser cookies to disk for session reuse.
    /// </summary>
    private async Task SaveSessionAsync()
    {
        var cookies = await _page.Context.CookiesAsync();
        await File.WriteAllTextAsync(_sessionFile,
            JsonSerializer.Serialize(cookies, new JsonSerializerOptions { WriteIndented = true }));
    }

    // -------------------------------------------------------------------------
    // Existing scraping methods below — unchanged
    // -------------------------------------------------------------------------

    public async Task<List<Guideline>> ScrapeGuidelines()
    {
        await RetryHelper.ExecuteAsync(
            () => _page.WaitForSelectorAsync(".product_pod"),
            "Waiting for product elements to load");

        var elements = await _page.QuerySelectorAllAsync(".product_pod h3 a");
        var guidelines = new List<Guideline>();

        foreach (var element in elements)
        {
            var title = await element.InnerTextAsync();
            var url = await element.GetAttributeAsync("href");
            guidelines.Add(new Guideline
            {
                GuidelineCode = Guid.NewGuid().ToString(),
                Title = title.Trim(),
                Category = "Template Category",
                Step = "1",
                Status = "Active",
                Dated = DateTime.Now,
                Summary = $"Template guideline: {title}",
                SourceUrl = url ?? ""
            });
        }

        return guidelines;
    }

    public async Task<List<GuidelineDocument>> ScrapeGuidelineDocuments()
    {
        await RetryHelper.ExecuteAsync(
            () => _page.WaitForSelectorAsync(".product_pod img"),
            "Waiting for image elements to load");

        var elements = await _page.QuerySelectorAllAsync(".product_pod img");
        var documents = new List<GuidelineDocument>();

        foreach (var element in elements)
        {
            var title = await element.GetAttributeAsync("alt");
            var url = await element.GetAttributeAsync("src");
            documents.Add(new GuidelineDocument
            {
                GuidelineCode = Guid.NewGuid().ToString(),
                DocumentTitle = title ?? "Untitled",
                DocumentUrl = (url != null && url.StartsWith("http")) ? url : $"https://books.toscrape.com{url}",
                DocumentType = "Image",
                FileFormat = "jpg"
            });
        }

        return documents;
    }
}