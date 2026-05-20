using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Track request counts per IP for rate limiting simulation
var requestCounts = new Dictionary<string, (int Count, DateTime Window)>();
var blockedIps = new HashSet<string>();

// -----------------------------------------------------------------------
// Tier 1 test — no protection, just returns content
// -----------------------------------------------------------------------
app.MapGet("/tier1", () =>
{
    return Results.Text("""
        <html>
        <body>
            <h1>Tier 1 — No Protection</h1>
            <p class='content'>You passed with no protection needed.</p>
        </body>
        </html>
    """, "text/html");
});

// -----------------------------------------------------------------------
// Tier 2 test — checks User-Agent and headers look realistic
// -----------------------------------------------------------------------
app.MapGet("/tier2", (HttpContext ctx) =>
{
    var userAgent = ctx.Request.Headers["User-Agent"].ToString();
    var acceptLanguage = ctx.Request.Headers["Accept-Language"].ToString();
    var secFetchMode = ctx.Request.Headers["Sec-Fetch-Mode"].ToString();

    var issues = new List<string>();

    if (userAgent.Contains("HeadlessChrome") || userAgent.Contains("Playwright"))
        issues.Add("User-Agent reveals automation");

    if (string.IsNullOrEmpty(acceptLanguage))
        issues.Add("Missing Accept-Language header");

    if (string.IsNullOrEmpty(secFetchMode))
        issues.Add("Missing Sec-Fetch-Mode header");

    if (issues.Count > 0)
        return Results.Text($"""
            <html><body>
            <h1>Tier 2 — BLOCKED</h1>
            <ul>{string.Join("", issues.Select(i => $"<li>{i}</li>"))}</ul>
            </body></html>
        """, "text/html", statusCode: 403);

    return Results.Text("""
        <html><body>
        <h1>Tier 2 — PASSED</h1>
        <p class='content'>Headers look realistic.</p>
        </body></html>
    """, "text/html");
});

// -----------------------------------------------------------------------
// Tier 3 test — rate limiting + JS fingerprint check via cookie
// -----------------------------------------------------------------------
app.MapGet("/tier3", (HttpContext ctx) =>
{
    var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Rate limit: more than 5 requests in 10 seconds = blocked
    var now = DateTime.UtcNow;
    if (requestCounts.TryGetValue(ip, out var entry))
    {
        if ((now - entry.Window).TotalSeconds < 10)
        {
            if (entry.Count >= 5)
            {
                blockedIps.Add(ip);
                return Results.Text("""
                    <html><body>
                    <h1>Rate Limited</h1>
                    <p>Too many requests. You have been blocked.</p>
                    </body></html>
                """, "text/html", statusCode: 429);
            }
            requestCounts[ip] = (entry.Count + 1, entry.Window);
        }
        else
        {
            requestCounts[ip] = (1, now); // reset window
        }
    }
    else
    {
        requestCounts[ip] = (1, now);
    }

    // Check for fingerprint cookie set by JS on first visit
    var fingerprintCookie = ctx.Request.Cookies["fp_verified"];
    if (fingerprintCookie != "true")
    {
        // First visit — serve JS challenge page
        return Results.Text("""
            <html>
            <head>
            <script>
                // Simulate fingerprint checks
                var checks = {
                    webdriver: navigator.webdriver,
                    plugins: navigator.plugins.length,
                    languages: navigator.languages.length,
                    hardwareConcurrency: navigator.hardwareConcurrency,
                    chrome: !!window.chrome
                };

                var passed = !checks.webdriver
                    && checks.plugins > 0
                    && checks.languages > 0
                    && checks.hardwareConcurrency > 1
                    && checks.chrome;

                if (passed) {
                    document.cookie = "fp_verified=true; path=/";
                    setTimeout(() => window.location.reload(), 500);
                } else {
                    document.getElementById('result').innerHTML =
                        '<h1>BLOCKED — Fingerprint failed</h1>'
                        + '<pre>' + JSON.stringify(checks, null, 2) + '</pre>';
                }
            </script>
            </head>
            <body>
                <div id='result'><p>Running fingerprint check...</p></div>
            </body>
            </html>
        """, "text/html");
    }

    return Results.Text("""
        <html><body>
        <h1>Tier 3 — PASSED</h1>
        <p class='content'>Rate limit and fingerprint checks passed.</p>
        </body></html>
    """, "text/html");
});

// -----------------------------------------------------------------------
// Honeypot test — hidden link that only bots follow
// -----------------------------------------------------------------------
app.MapGet("/honeypot", (HttpContext ctx) =>
{
    var referer = ctx.Request.Headers["Referer"].ToString();
    return Results.Text($"""
        <html><body>
        <h1>BLOCKED — Honeypot triggered</h1>
        <p>You followed a hidden link. Bots do this, humans don't.</p>
        <p>Referer: {referer}</p>
        </body></html>
    """, "text/html", statusCode: 403);
});

app.MapGet("/page-with-honeypot", () => Results.Text("""
    <html>
    <head>
    <style>
        .trap { display: none; visibility: hidden; }
    </style>
    </head>
    <body>
        <h1>Normal Page</h1>
        <p class='content'>This is the real content you want.</p>

        <!-- Honeypot: invisible to humans, visible to scrapers reading raw HTML -->
        <a class='trap' href='/honeypot' tabindex='-1' aria-hidden='true'>
            Click here for more
        </a>
    </body>
    </html>
""", "text/html"));

// -----------------------------------------------------------------------
// Results dashboard — shows all test URLs
// -----------------------------------------------------------------------
app.MapGet("/", () => Results.Text("""
    <html>
    <body>
        <h1>Anti-Bot Test Server</h1>
        <ul>
            <li><a href='/tier1'>/tier1 — No protection</a></li>
            <li><a href='/tier2'>/tier2 — Header checks</a></li>
            <li><a href='/tier3'>/tier3 — Rate limit + JS fingerprint</a></li>
            <li><a href='/page-with-honeypot'>/page-with-honeypot — Honeypot trap</a></li>
        </ul>
    </body>
    </html>
""", "text/html"));

app.Run("http://localhost:5000");