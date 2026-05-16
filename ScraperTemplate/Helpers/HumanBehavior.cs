using Microsoft.Playwright;

namespace ScraperTemplate.Helpers;

public static class HumanBehavior
{
    private static readonly Random _random = new Random();

    /// <summary>
    /// Waits a random amount of time between min and max milliseconds.
    /// Use between page navigations and clicks to avoid inhuman speed.
    /// </summary>
    public static async Task PauseAsync(int minMs = 800, int maxMs = 2500)
    {
        var delay = _random.Next(minMs, maxMs);
        await Task.Delay(delay);
    }

    /// <summary>
    /// Simulates reading time based on content length before moving on.
    /// </summary>
    public static async Task ReadingPauseAsync(string content)
    {
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var ms = Math.Clamp(words * 100, 500, 5000);
        await Task.Delay(ms + _random.Next(0, 500));
    }

    /// <summary>
    /// Moves the mouse naturally to a random point within an element before clicking.
    /// Much more human-like than a direct programmatic click.
    /// </summary>
    public static async Task HumanClickAsync(IPage page, string selector)
    {
        var element = await page.QuerySelectorAsync(selector)
            ?? throw new InvalidOperationException($"Element not found: {selector}");

        await MoveToElementAsync(page, element);
        await PauseAsync(80, 250);
        await element.ClickAsync();
    }

    /// <summary>
    /// Overload that accepts an already-retrieved element handle directly.
    /// </summary>
    public static async Task HumanClickAsync(IPage page, IElementHandle element)
    {
        await MoveToElementAsync(page, element);
        await PauseAsync(80, 250);
        await element.ClickAsync();
    }

    /// <summary>
    /// Types text character by character with randomized delays between keystrokes,
    /// mimicking how a human types rather than instantly filling a field.
    /// Use instead of FillAsync() when sites check for human typing patterns.
    /// </summary>
    public static async Task HumanTypeAsync(IPage page, string selector, string text)
    {
        var element = await page.QuerySelectorAsync(selector)
            ?? throw new InvalidOperationException($"Element not found: {selector}");

        await element.ClickAsync(); // focus the field first
        await PauseAsync(100, 300);

        foreach (var character in text)
        {
            await page.Keyboard.TypeAsync(character.ToString());

            // Occasional longer pause simulating brief hesitation
            var delay = _random.Next(0, 10) == 0
                ? _random.Next(300, 600)  // 10% chance of a hesitation pause
                : _random.Next(50, 180);  // normal keystroke delay

            await Task.Delay(delay);
        }
    }

    /// <summary>
    /// Scrolls the page gradually in a human-like manner rather than jumping instantly.
    /// Useful for pages with lazy-loaded content or scroll-based anti-bot checks.
    /// </summary>
    public static async Task HumanScrollAsync(IPage page, int targetY = -1)
    {
        var currentY = await page.EvaluateAsync<int>("window.scrollY");
        var pageHeight = await page.EvaluateAsync<int>("document.body.scrollHeight");
        var destination = targetY == -1 ? pageHeight : targetY;

        while (currentY < destination)
        {
            // Scroll in random increments between 100-400px
            var scrollAmount = _random.Next(100, 400);
            currentY = Math.Min(currentY + scrollAmount, destination);

            await page.EvaluateAsync($"window.scrollTo(0, {currentY})");
            await Task.Delay(_random.Next(50, 150));
        }
    }

    /// <summary>
    /// Moves the mouse to a random point within the bounds of an element.
    /// </summary>
    private static async Task MoveToElementAsync(IPage page, IElementHandle element)
    {
        var box = await element.BoundingBoxAsync();
        if (box == null) return;

        // Aim for a random point in the middle 40% of the element
        var x = box.X + box.Width * (0.3 + _random.NextDouble() * 0.4);
        var y = box.Y + box.Height * (0.3 + _random.NextDouble() * 0.4);

        await page.Mouse.MoveAsync((float)x, (float)y);
    }
}