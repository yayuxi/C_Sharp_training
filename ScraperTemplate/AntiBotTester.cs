using Microsoft.Playwright;
using ScraperTemplate.Helpers;
using System.Linq;
using System.Threading.Tasks;

namespace ScraperTemplate;

public class AntiBotTester
{
    private readonly IPage _page;

    public AntiBotTester(IPage page)
    {
        _page = page;
    }

    public async Task RunAllTestsAsync()
    {
        var tests = new[]
        {
            ("Tier 1 — No protection",      "http://localhost:5000/tier1"),
            ("Tier 2 — Header checks",      "http://localhost:5000/tier2"),
            ("Tier 3 — Fingerprint+rate",   "http://localhost:5000/tier3"),
            ("Honeypot detection",          "http://localhost:5000/page-with-honeypot"),
            ("Real fingerprint report",     "https://bot.sannysoft.com"),
            ("Real header check",           "https://httpbin.org/headers"),
        };

        foreach (var (name, url) in tests)
        {
            Console.WriteLine($"\n{Enumerable.Repeat("=", 50)}");
            Console.WriteLine($"Test: {name}");

            await RetryHelper.ExecuteWithEscalationAsync(
                async () =>
                {
                    await _page.GotoAsync(url, new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.NetworkIdle,
                        Timeout = 15000
                    });
                },
                _page, url, name);

            var content = await _page.ContentAsync();
            var passed = content.Contains("PASSED") || content.Contains("passed");
            var blocked = content.Contains("BLOCKED") || content.Contains("blocked")
                                                      || content.Contains("Rate Limited");

            Console.WriteLine(passed ? $"[PASS] {name}"
                : blocked ? $"[FAIL] {name} — blocked"
                : $"[UNKNOWN] {name} — check screenshot");

            await _page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = $"antibot_{name.Replace(" ", "_").Replace("—", "")}.png",
                FullPage = true
            });
        }
    }
}