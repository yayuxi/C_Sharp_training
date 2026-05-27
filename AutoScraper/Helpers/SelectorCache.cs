using System.Text.Json;

namespace ScraperTemplate.Helpers;

/// <summary>
/// Caches selectors returned by the AI so the model is only called
/// when the site structure changes, not on every scrape run.
/// </summary>
public class SelectorCache
{
    private readonly string _cacheFile;
    private Dictionary<string, CacheEntry> _cache;

    public SelectorCache(string cacheFile = "selector_cache.json")
    {
        _cacheFile = cacheFile;
        _cache = LoadCache();
    }

    public bool TryGet(string url, out List<ElementSummary> summaries)
    {
        var key = NormalizeUrl(url);
        if (_cache.TryGetValue(key, out var entry))
        {
            summaries = entry.Elements;
            Console.WriteLine($"[Cache] Loaded {summaries.Count} selectors for {key}");
            return true;
        }

        summaries = [];
        return false;
    }

    public void Set(string url, List<ElementSummary> summaries)
    {
        var key = NormalizeUrl(url);
        _cache[key] = new CacheEntry
        {
            Elements = summaries,
            CachedAt = DateTime.UtcNow
        };
        SaveCache();
        Console.WriteLine($"[Cache] Saved {summaries.Count} selectors for {key}");
    }

    public void Invalidate(string url)
    {
        var key = NormalizeUrl(url);
        _cache.Remove(key);
        SaveCache();
        Console.WriteLine($"[Cache] Invalidated cache for {key}");
    }

    private static string NormalizeUrl(string url) =>
        url.ToLower().TrimEnd('/');

    private Dictionary<string, CacheEntry> LoadCache()
    {
        if (!File.Exists(_cacheFile)) return new();
        try
        {
            var json = File.ReadAllText(_cacheFile);
            return JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    private void SaveCache()
    {
        File.WriteAllText(_cacheFile,
            JsonSerializer.Serialize(_cache,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private class CacheEntry
    {
        public List<ElementSummary> Elements { get; set; } = [];
        public DateTime CachedAt { get; set; }
    }
}