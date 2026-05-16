using Microsoft.Playwright;

namespace ScraperTemplate.Helpers;

public static class RetryHelper
{
    private const int MaxRetries = 3;
    private const int BaseDelayMs = 3000;

    /// <summary>
    /// Executes an action with escalating anti-bot protection on each retry.
    /// Tier 1: Raw request — fastest, works on unprotected sites
    /// Tier 2: Human-like behavior — delays, mouse movement, realistic headers
    /// Tier 3: Full stealth — all tier 2 measures + proxy + JS fingerprint spoofing
    /// </summary>
    public static async Task<T> ExecuteWithEscalationAsync<T>(
        Func<Task<T>> action,
        IPage page,
        string url,
        string context = "")
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"[Retry] Attempt {attempt}/{MaxRetries} — " +
                                  $"Tier {attempt} protection — {context}");

                await ApplyTierAsync(page, attempt);
                var result = await action();
                await CheckForBlockAsync(page);
                return result;
            }
            catch (BlockedException ex)
            {
                Console.WriteLine($"[Blocked] {ex.Message} — escalating to tier {attempt + 1}");
                if (attempt == MaxRetries) throw;
                await Task.Delay(BaseDelayMs * attempt);
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Console.WriteLine($"[Error] Attempt {attempt} failed — {ex.Message}");
                await Task.Delay(BaseDelayMs * attempt);
            }
        }

        throw new ScraperException($"All {MaxRetries} attempts failed for: {context}");
    }

    /// <summary>
    /// Overload for void actions.
    /// </summary>
    public static async Task ExecuteWithEscalationAsync(
        Func<Task> action,
        IPage page,
        string url,
        string context = "")
    {
        await ExecuteWithEscalationAsync<bool>(async () =>
        {
            await action();
            return true;
        }, page, url, context);
    }

    /// <summary>
    /// Simple retry without escalation — used for non-navigation actions
    /// like waiting for selectors or clicking elements.
    /// </summary>
    public static async Task ExecuteAsync(
        Func<Task> action,
        string context = "")
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Console.WriteLine($"[Retry {attempt}/{MaxRetries}] {context} — {ex.Message}");
                await Task.Delay(BaseDelayMs);
            }
        }
        await action();
    }

    // -------------------------------------------------------------------------
    // Tier application
    // -------------------------------------------------------------------------

    private static async Task ApplyTierAsync(IPage page, int tier)
    {
        switch (tier)
        {
            case 1:
                await ApplyTier1Async(page);
                break;
            case 2:
                await ApplyTier2Async(page);
                break;
            case 3:
                await ApplyTier3Async(page);
                break;
        }
    }

    /// <summary>
    /// Tier 1 — No protection. Raw browser request.
    /// Works on most public/government sites with no bot detection.
    /// </summary>
    private static Task ApplyTier1Async(IPage page)
    {
        Console.WriteLine("[Tier 1] Raw request — no anti-bot measures");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tier 2 — Human-like behavior.
    /// Realistic headers, randomized timing, mouse movement simulation.
    /// </summary>
    private static async Task ApplyTier2Async(IPage page)
    {
        Console.WriteLine("[Tier 2] Applying human-like behavior...");

        // Set realistic browser headers
        await page.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            ["Accept-Language"] = "en-GB,en;q=0.9",
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            ["Accept-Encoding"] = "gzip, deflate, br",
            ["Connection"] = "keep-alive",
            ["Upgrade-Insecure-Requests"] = "1",
            ["Sec-Fetch-Dest"] = "document",
            ["Sec-Fetch-Mode"] = "navigate",
            ["Sec-Fetch-Site"] = "none",
            ["Sec-Fetch-User"] = "?1"
        });

        // Simulate pre-navigation human behavior
        await HumanBehavior.PauseAsync(500, 1500);
        await SimulateIdleMouseMovementAsync(page);
        await HumanBehavior.PauseAsync(300, 800);
    }

    /// <summary>
    /// Tier 3 — Full stealth mode.
    /// All tier 2 measures + JS fingerprint spoofing + proxy routing.
    /// Note: proxy requires a new browser context — we recreate the page here.
    /// </summary>
    private static async Task ApplyTier3Async(IPage page)
    {
        Console.WriteLine("[Tier 3] Applying full stealth mode...");

        // Apply all tier 2 measures first
        await ApplyTier2Async(page);

        // JS fingerprint spoofing — hides Playwright automation signals
        await page.AddInitScriptAsync("""
            // Remove webdriver flag
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });

            // Spoof plugins to look like a real browser
            Object.defineProperty(navigator, 'plugins', {
                get: () => [
                    { name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer' },
                    { name: 'Chrome PDF Viewer', filename: 'mhjfbmdgcfjbbpaeojofohoefgiehjai' },
                    { name: 'Native Client', filename: 'internal-nacl-plugin' }
                ]
            });

            // Spoof language settings
            Object.defineProperty(navigator, 'languages', {
                get: () => ['en-GB', 'en', 'da']
            });

            // Add chrome runtime object (missing in automated browsers)
            if (!window.chrome) {
                window.chrome = {
                    runtime: {
                        onMessage: { addListener: () => {} },
                        connect: () => {}
                    }
                };
            }

            // Spoof hardware concurrency (CPU cores)
            Object.defineProperty(navigator, 'hardwareConcurrency', { get: () => 8 });

            // Spoof device memory
            Object.defineProperty(navigator, 'deviceMemory', { get: () => 8 });

            // Prevent canvas fingerprinting by adding subtle noise
            const originalToDataURL = HTMLCanvasElement.prototype.toDataURL;
            HTMLCanvasElement.prototype.toDataURL = function(type) {
                const context = this.getContext('2d');
                if (context) {
                    const imageData = context.getImageData(0, 0, this.width, this.height);
                    for (let i = 0; i < imageData.data.length; i += 100) {
                        imageData.data[i] ^= 1; // flip one bit occasionally
                    }
                    context.putImageData(imageData, 0, 0);
                }
                return originalToDataURL.apply(this, arguments);
            };
        """);

        // Apply proxy by recreating the browser context
        // NOTE: This requires ProxySettings.Default to be configured
        try
        {
            await RecreateContextWithProxyAsync(page);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Tier 3] Proxy setup failed — continuing without proxy: {ex.Message}");
        }

        // Longer human delay before proceeding
        await HumanBehavior.PauseAsync(1500, 3000);
        await SimulateIdleMouseMovementAsync(page);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Recreates the browser context with proxy settings and navigates back to the current URL.
    /// </summary>
    private static async Task RecreateContextWithProxyAsync(IPage page)
    {
        var currentUrl = page.Url;
        var browser = page.Context.Browser
            ?? throw new ScraperException("Cannot access browser from page context");

        Console.WriteLine("[Tier 3] Recreating browser context with proxy...");

        // Close the old context and open a new one with proxy
        var proxy = ProxySettings.Default;
        var newContext = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            Proxy = new Proxy
            {
                Server = proxy.Server,
                Username = proxy.Username,
                Password = proxy.Password
            },
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                        "AppleWebKit/537.36 (KHTML, like Gecko) " +
                        "Chrome/124.0.0.0 Safari/537.36",
            Locale = "en-GB",
            TimezoneId = "Europe/London",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
        });

        // Navigate the new context to where we were
        var newPage = await newContext.NewPageAsync();
        await newPage.GotoAsync(currentUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        Console.WriteLine("[Tier 3] Proxy context active");
    }

    /// <summary>
    /// Moves the mouse in a few random arcs to simulate idle human movement.
    /// </summary>
    private static async Task SimulateIdleMouseMovementAsync(IPage page)
    {
        var random = new Random();
        var viewport = page.ViewportSize;
        if (viewport == null) return;

        // Make 3-5 random mouse movements across the viewport
        int movements = random.Next(3, 6);
        for (int i = 0; i < movements; i++)
        {
            var x = random.Next(100, viewport.Width - 100);
            var y = random.Next(100, viewport.Height - 100);
            await page.Mouse.MoveAsync(x, y);
            await Task.Delay(random.Next(100, 400));
        }
    }

    /// <summary>
    /// Checks the loaded page for signs of being rate-limited or blocked.
    /// Throws BlockedException if detected so the retry loop can escalate.
    /// </summary>
    private static async Task CheckForBlockAsync(IPage page)
    {
        var url = page.Url.ToLower();
        var content = await page.ContentAsync();
        var contentLower = content.ToLower();

        var blockSignals = new[]
        {
            "captcha",
            "rate limit",
            "too many requests",
            "access denied",
            "blocked",
            "unusual traffic",
            "bot detected",
            "security check"
        };

        var urlBlockSignals = new[] { "blocked", "captcha", "denied", "error" };

        if (blockSignals.Any(s => contentLower.Contains(s)) ||
            urlBlockSignals.Any(s => url.Contains(s)))
        {
            var signal = blockSignals.FirstOrDefault(s => contentLower.Contains(s))
                         ?? urlBlockSignals.First(s => url.Contains(s));
            throw new BlockedException($"Block detected: '{signal}' found on {page.Url}");
        }
    }
}