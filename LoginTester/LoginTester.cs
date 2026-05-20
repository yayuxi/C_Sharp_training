using Microsoft.Playwright;

namespace LoginTester;

public class LoginTester
{
    private readonly IPage _page;
    private static readonly Random _random = new Random();

    public LoginTester(IPage page)
    {
        _page = page;
    }

    public async Task<LoginResult> TestLoginAsync(LoginTarget target)
    {
        try
        {
            await _page.GotoAsync(target.LoginUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 15000
            });

            Console.WriteLine($"[Login] Loaded: {_page.Url}");
            await TakeScreenshotAsync("01_initial_page");

            // Detect form type upfront
            var formType = await DetectFormTypeAsync();
            Console.WriteLine($"[Login] Form type detected: {formType}");

            switch (formType)
            {
                case FormType.SingleStep:
                    await HandleSingleStepAsync(target.Username, target.Password);
                    break;
                case FormType.MultiStep:
                    await HandleMultiStepAsync(target.Username, target.Password);
                    break;
                case FormType.Unknown:
                    return new LoginResult
                    {
                        Success = false,
                        FailureReason = "Could not detect login form structure",
                        LandedUrl = _page.Url
                    };
            }

            // Wait for navigation to settle
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await TakeScreenshotAsync("04_after_login");

            // Verify success
            var success = await VerifyLoginAsync(target.SuccessIndicator);

            return new LoginResult
            {
                Success = success,
                FailureReason = success ? "" : "Success indicator not found after login",
                LandedUrl = _page.Url
            };
        }
        catch (Exception ex)
        {
            await TakeScreenshotAsync("error_state");
            return new LoginResult
            {
                Success = false,
                FailureReason = ex.Message,
                LandedUrl = _page.Url
            };
        }
    }

    // -------------------------------------------------------------------------
    // Form type detection
    // -------------------------------------------------------------------------

    private async Task<FormType> DetectFormTypeAsync()
    {
        // Check if both fields are visible at the same time = single step
        var usernameVisible = await IsUsernameFieldVisibleAsync();
        var passwordVisible = await IsPasswordFieldVisibleAsync();

        if (usernameVisible && passwordVisible)
            return FormType.SingleStep;

        if (usernameVisible && !passwordVisible)
            return FormType.MultiStep;

        return FormType.Unknown;
    }

    private async Task<bool> IsUsernameFieldVisibleAsync()
    {
        var selectors = new[]
        {
            "input[type='email']",
            "input[type='text'][name*='user' i]",
            "input[type='text'][name*='email' i]",
            "input[type='text'][id*='user' i]",
            "input[type='text'][id*='email' i]",
            "input[autocomplete='username']",
            "input[autocomplete='email']",
            // Broad fallback — any visible text input that isn't password
            "input[type='text']:not([type='password'])",
            "input:not([type='password']):not([type='hidden'])"
                + ":not([type='checkbox']):not([type='radio'])"
                + ":not([type='submit']):not([type='button'])"
        };

        foreach (var selector in selectors)
        {
            var element = await _page.QuerySelectorAsync(selector);
            if (element != null && await element.IsVisibleAsync())
                return true;
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

        // Wait for password field to appear
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
        // Ordered from most specific to most general — stops at first match
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
            // Broad fallback — first visible non-password text input
            "input[type='text']:not([type='password'])"
        };

        await FillFirstMatchAsync(selectors, username, "username", excludePassword: true);
    }

    private async Task FillPasswordAsync(string password)
    {
        // type='password' is universal — no need for fallbacks
        var element = await _page.QuerySelectorAsync("input[type='password']")
            ?? throw new Exception("Password field not found");

        await element.FillAsync(password);
        Console.WriteLine("[Login] Filled password via type='password'");
    }

    private async Task FillFirstMatchAsync(
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

        throw new Exception($"Could not find {fieldName} field on page");
    }

    // -------------------------------------------------------------------------
    // Submit
    // -------------------------------------------------------------------------

    private async Task ClickSubmitAsync()
    {
        var buttonPatterns = new[]
            { "sign in", "log in", "login", "next", "continue", "submit" };

        // Try all buttons and inputs
        var candidates = await _page.QuerySelectorAllAsync(
            "button, input[type='submit']");

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

        // Fallback: single visible submit button even if text doesn't match
        foreach (var candidate in candidates)
        {
            if (await candidate.IsVisibleAsync())
            {
                Console.WriteLine("[Login] Clicking first visible submit button");
                await candidate.ClickAsync();
                return;
            }
        }

        Console.WriteLine("[Login] No submit button found, pressing Enter");
        await _page.Keyboard.PressAsync("Enter");
    }

    // -------------------------------------------------------------------------
    // Verification and utilities
    // -------------------------------------------------------------------------

    private async Task<bool> VerifyLoginAsync(string successIndicator)
    {
        // Check site-specific success indicator first
        if (!string.IsNullOrWhiteSpace(successIndicator))
        {
            try
            {
                await _page.WaitForSelectorAsync(successIndicator,
                    new PageWaitForSelectorOptions { Timeout = 5000 });
                Console.WriteLine($"[Login] Success indicator found: {successIndicator}");
                return true;
            }
            catch
            {
                Console.WriteLine($"[Login] Success indicator not found: {successIndicator}");
            }
        }

        // Fallback: check URL no longer contains login-related keywords
        var url = _page.Url.ToLower();
        var isStillOnLoginPage = new[] { "login", "signin", "sign-in", "auth" }
            .Any(url.Contains);

        Console.WriteLine($"[Login] URL check — still on login page: {isStillOnLoginPage}");
        return !isStillOnLoginPage;
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
    {
        await Task.Delay(_random.Next(minMs, maxMs));
    }
}

public enum FormType
{
    SingleStep,
    MultiStep,
    Unknown
}