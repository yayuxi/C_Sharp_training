using Microsoft.Playwright;
using ScraperTemplate.Helpers;
using System.Text.Json;

namespace ScraperTemplate.Scraper;

/// <summary>
/// Handles all login functionality including session persistence,
/// form detection, and multi-step login flows.
/// No changes needed here when building a new scraper —
/// override IsLoggedInAsync in Scraper.cs if the default URL check isn't enough.
/// </summary>
public class LoginHandler
{
    private readonly IPage _page;
    private readonly string _sessionFile = "session.json";
    private static readonly Random _random = new Random();

    public LoginHandler(IPage page)
    {
        _page = page;
    }

    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Logs in to the current page using the provided credentials.
    /// Restores a saved session if one exists and is still valid.
    /// Call this after navigating to the login page.
    /// </summary>
    public async Task LoginAsync(string username, string password,
        Func<Task<bool>> isLoggedInCheck)
    {
        if (await TryRestoreSessionAsync(isLoggedInCheck)) return;

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
                    throw new ScraperException(
                        "[Login] Could not detect login form structure");
            }

            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await TakeScreenshotAsync("04_after_login");

            if (!await isLoggedInCheck())
                throw new ScraperException(
                    "[Login] Login failed — still on login page or credentials rejected");

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

        var candidates = await _page.QuerySelectorAllAsync(
            "button, input[type='submit']");

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

        // Fallback: first visible button regardless of text
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

    private async Task<bool> TryRestoreSessionAsync(Func<Task<bool>> isLoggedInCheck)
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

            if (await isLoggedInCheck())
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
    // Utilities
    // -------------------------------------------------------------------------

    private async Task TakeScreenshotAsync(string label)
    {
        var path = $"ScreenShots/screenshot_{label}_{DateTime.Now:HHmmss}.png";
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true
        });
        Console.WriteLine($"[Screenshot] {path}");
    }

    private static async Task PauseAsync(int minMs, int maxMs)
        => await Task.Delay(_random.Next(minMs, maxMs));
}

public enum FormType { SingleStep, MultiStep, Unknown }