
using Microsoft.Playwright;
using ScraperTemplate.Helpers;
using ScraperTemplate.Models;
using System.Text.Json;

namespace ScraperTemplate.Scraper;

public class Scraper
{
    private readonly IPage _page;
    private readonly string _sessionFile = "session.json";
    private static readonly Random _random = new Random();

    public Scraper(IPage page)
    {
        _page = page;
    }

    // -------------------------------------------------------------------------
    // Login
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempts to log in to the current page using the provided credentials.
    /// Restores a saved session if available, otherwise performs a fresh login.
    /// Call this after GotoAsync() on the login page.
    /// </summary>
    public async Task LoginAsync(string username, string password)
    {
        if (await TryRestoreSessionAsync()) return;

        await RetryHelper.ExecuteAsync(async () =>
        {
            var formType = await DetectFormTypeAsync();
            Console.WriteLine($"[Login] Form type detected: {formType}");

            switch (formType)
            {
                case FormType.SingleStep:
                    await HandleSingleStepAsync(username, password);
                    break;
                case FormType.MultiStep:
                    await HandleMultiStepAsync(username, password);
                    break;
                case FormType.Unknown:
                    throw new ScraperException("[Login] Could not detect login form structure");
            }

            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await TakeScreenshotAsync("04_after_login");

            if (!await IsLoggedInAsync())
                throw new ScraperException("[Login] Login failed — still on login page or credentials rejected");

        }, "[Login] Attempting login");

        await SaveSessionAsync();
        Console.WriteLine("[Login] Login successful, session saved");
    }

    // -------------------------------------------------------------------------
    // Form type detection
    // -------------------------------------------------------------------------

    private async Task<FormType> DetectFormTypeAsync()
    {
        var usernameVisible = await IsUsernameFieldVisibleAsync();
        var passwordVisible = await IsPasswordFieldVisibleAsync();

        if (usernameVisible && passwordVisible) return FormType.SingleStep;
        if (usernameVisible && !passwordVisible) return FormType.MultiStep;
        return FormType.Unknown;
    }

    private async Task<bool> IsUsernameFieldVisibleAsync()
    {
        var selectors = new[]
        {
            "input[type='email']",
            "input[autocomplete='username']",
            "input[autocomplete='email']",
            "input[name*='user' i]",
            "input[name*='email' i]",
            "input[id*='user' i]",
            "input[id*='email' i]",
            "input[placeholder*='email' i]",
            "input[placeholder*='username' i]",
            "input[type='text']:not([type='password'])"
        };

        foreach (var selector in selectors)
        {
            var element = await _page.QuerySelectorAsync(selector);
            if (element != null && await element.IsVisibleAsync()) return true;
        }

        return false;
    }

    private async Task<bool> IsPasswordFieldVisibleAsync()
    {
        var element = await _page.QuerySelectorAsync("input[type='password']");
        return element != null && await element.IsVisibleAsync();
    }

    // -------------------------------------------------------------------------
    // Form handlers
    // -------------------------------------------------------------------------

    private async Task HandleSingleStepAsync(string username, string password)
    {
        Console.WriteLine("[Login] Handling single-step form");
        await FillUsernameAsync(username);
        await TakeScreenshotAsync("02_username_filled");
        await FillPasswordAsync(password);
        await TakeScreenshotAsync("03_password_filled");
        await ClickSubmitAsync();
    }

    private async Task HandleMultiStepAsync(string username, string password)
    {
        Console.WriteLine("[Login] Handling multi-step form — step 1: username");
        await FillUsernameAsync(username);
        await TakeScreenshotAsync("02_username_filled");
        await ClickSubmitAsync();

        Console.WriteLine("[Login] Waiting for password field...");
        await _page.WaitForSelectorAsync("input[type='password']",
            new PageWaitForSelectorOptions { Timeout = 10000 });
        await PauseAsync(600, 1200);
        await TakeScreenshotAsync("03_password_step");

        Console.WriteLine("[Login] Step 2: password");
        await FillPasswordAsync(password);
        await TakeScreenshotAsync("03b_password_filled");
        await ClickSubmitAsync();
    }

    // -------------------------------------------------------------------------
    // Field filling
    // -------------------------------------------------------------------------

    private async Task FillUsernameAsync(string username)
    {
        var selectors = new[]
        {
            "input[type='email']",
            "input[autocomplete='username']",
            "input[autocomplete='email']",
            "input[name='email']",
            "input[name='username']",
            "input[name='user']",
            "input[name='login']",
            "input[id*='email' i]",
            "input[id*='user' i]",
            "input[placeholder*='email' i]",
            "input[placeholder*='username' i]",
            "input[type='text']:not([type='password'])"
        };

        await FillFirstVisibleAsync(selectors, username, "username", excludePassword: true);
    }

    private async Task FillPasswordAsync(string password)
    {
        var element = await _page.QuerySelectorAsync("input[type='password']")
            ?? throw new ScraperException("[Login] Password field not found");

        await element.FillAsync(password);
        Console.WriteLine("[Login] Filled password via type='password'");
    }

    private async Task FillFirstVisibleAsync(
        string[] selectors,
        string value,
        string fieldName,
        bool excludePassword = false)
    {
        foreach (var selector in selectors)
        {
            var element = await _page.QuerySelectorAsync(selector);
            if (element == null) continue;
            if (!await element.IsVisibleAsync()) continue;

            if (excludePassword)
            {
                var type = await element.GetAttributeAsync("type") ?? "";
                if (type == "password") continue;
            }

            await element.FillAsync(value);
            Console.WriteLine($"[Login] Filled {fieldName} via '{selector}'");
            return;
        }

        throw new ScraperException($"[Login] Could not find {fieldName} field on page");
    }

    // -------------------------------------------------------------------------
    // Submit
    // -------------------------------------------------------------------------

    private async Task ClickSubmitAsync()
    {
        var buttonPatterns = new[]
            { "sign in", "log in", "login", "next", "continue", "submit" };

        var candidates = await _page.QuerySelectorAllAsync("button, input[type='submit']");

        // Try to match by visible text first
        foreach (var candidate in candidates)
        {
            if (!await candidate.IsVisibleAsync()) continue;

            var text = (await candidate.InnerTextAsync()).Trim().ToLower();
            var value = (await candidate.GetAttributeAsync("value") ?? "").ToLower();

            if (buttonPatterns.Any(p => text.Contains(p) || value.Contains(p)))
            {
                Console.WriteLine($"[Login] Clicking submit: '{text}'");
                await candidate.ClickAsync();
                return;
            }
        }

        // Fallback: first visible submit button regardless of text
        foreach (var candidate in candidates)
        {
            if (await candidate.IsVisibleAsync())
            {
                Console.WriteLine("[Login] Clicking first visible button");
                await candidate.ClickAsync();
                return;
            }
        }

        Console.WriteLine("[Login] No submit button found, pressing Enter");
        await _page.Keyboard.PressAsync("Enter");
    }

    // -------------------------------------------------------------------------
    // Session persistence
    // -------------------------------------------------------------------------

    private async Task<bool> TryRestoreSessionAsync()
    {
        if (!File.Exists(_sessionFile)) return false;

        Console.WriteLine("[Login] Restoring saved session...");
        try
        {
            var cookies = JsonSerializer.Deserialize<List<Cookie>>(
                await File.ReadAllTextAsync(_sessionFile));

            if (cookies != null)
                await _page.Context.AddCookiesAsync(cookies);

            await _page.ReloadAsync(new PageReloadOptions
                { WaitUntil = WaitUntilState.NetworkIdle });

            if (await IsLoggedInAsync())
            {
                Console.WriteLine("[Login] Session restored successfully");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Login] Session restore failed — {ex.Message}");
        }

        Console.WriteLine("[Login] Saved session expired, logging in fresh...");
        File.Delete(_sessionFile);
        return false;
    }

    private async Task SaveSessionAsync()
    {
        var cookies = await _page.Context.CookiesAsync();
        await File.WriteAllTextAsync(_sessionFile,
            JsonSerializer.Serialize(cookies,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    // -------------------------------------------------------------------------
    // Verification and utilities
    // -------------------------------------------------------------------------

    /// <summary>
    /// Override this in a subclass to check for a site-specific post-login element.
    /// Default checks that the URL no longer contains login-related keywords.
    /// </summary>
    protected virtual async Task<bool> IsLoggedInAsync()
    {
        var url = _page.Url.ToLower();
        var isOnLoginPage = new[] { "login", "signin", "sign-in", "auth" }
            .Any(url.Contains);

        var postLoginElement = await _page.QuerySelectorAsync(
            "[class*='dashboard'], [class*='profile'], [class*='account'], " +
            "[href*='logout'], [href*='sign-out']");

        return !isOnLoginPage || postLoginElement != null;
    }

    private async Task TakeScreenshotAsync(string label)
    {
        var path = $"screenshot_{label}_{DateTime.Now:HHmmss}.png";
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true
        });
        Console.WriteLine($"[Screenshot] {path}");
    }

    private static async Task PauseAsync(int minMs, int maxMs)
        => await Task.Delay(_random.Next(minMs, maxMs));

    // -------------------------------------------------------------------------
    // Scraping — replace with site-specific logic
    // -------------------------------------------------------------------------

    public async Task<List<Guideline>> ScrapeGuidelines()
    {
        // await RetryHelper.ExecuteAsync(
        //     () => _page.WaitForSelectorAsync(".product_pod"),
        //     "Waiting for product elements to load");

        var elements = await _page.QuerySelectorAllAsync(".quote");
        var guidelines = new List<Guideline>();

        foreach (var element in elements)
        {
            var title = await element.InnerTextAsync();
            var url = await element.GetAttributeAsync("href");
            var tags = await element.QuerySelectorAllAsync(".tag");
            var tagsList = new List<string>();
            foreach (var tag in tags)                                   
                tagsList.Add(await tag.InnerTextAsync());
            guidelines.Add(new Guideline
            {
                GuidelineCode = Guid.NewGuid().ToString(),
                Title = title.Trim(),
                Category = string.Join("|", tagsList),
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
        // await RetryHelper.ExecuteAsync(
        //     () => _page.WaitForSelectorAsync(".product_pod img"),
        //     "Waiting for image elements to load");

        var elements = await _page.QuerySelectorAllAsync(".quote");
        var documents = new List<GuidelineDocument>();

        foreach (var element in elements)
        {
            var title = await element.GetAttributeAsync("alt");
            var url = await element.GetAttributeAsync("src");
            documents.Add(new GuidelineDocument
            {
                GuidelineCode = Guid.NewGuid().ToString(),
                DocumentTitle = title ?? "Untitled",
                DocumentUrl = (url != null && url.StartsWith("http"))
                    ? url
                    : $"https://books.toscrape.com{url}",
                DocumentType = "Image",
                FileFormat = "jpg"
            });
        }

        return documents;
    }
}

public enum FormType
{
    SingleStep,
    MultiStep,
    Unknown
}

/*
public class MyCustomScraper : Scraper
{
    public MyCustomScraper(IPage page) : base(page) { }

    protected override async Task<bool> IsLoggedInAsync()
    {
        // Check for a site-specific element that only appears when logged in
        var avatar = await _page.QuerySelectorAsync(".user-avatar");
        return avatar != null && await avatar.IsVisibleAsync();
    }
}*/