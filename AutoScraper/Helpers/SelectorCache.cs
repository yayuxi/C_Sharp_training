using System.Text.Json;

namespace ScraperTemplate.Helpers;

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
        if (_cache.TryGetValue(key, out var entry) && entry.Elements.Count > 0)
        {
            summaries = entry.Elements;
            Console.WriteLine($"[Cache] Loaded {summaries.Count} selectors for {key}");
            return true;
        }

        summaries = [];
        return false;
    }

    /// <summary>
    /// Merges new selectors into the cache rather than replacing existing ones.
    /// This way each run accumulates more selectors even with rate limiting.
    /// </summary>
    public void Merge(string url, List<ElementSummary> newElements)
    {
        var key = NormalizeUrl(url);

        if (!_cache.ContainsKey(key))
        {
            _cache[key] = new CacheEntry
            {
                Elements = newElements,
                LastUpdated = DateTime.UtcNow,
                TotalDiscovered = newElements.Count
            };
        }
        else
        {
            var existing = _cache[key].Elements;

            // Add only elements not already cached (by selector)
            var existingSelectors = existing.Select(e => e.Selector).ToHashSet();
            var toAdd = newElements
                .Where(e => !existingSelectors.Contains(e.Selector))
                .ToList();

            existing.AddRange(toAdd);
            _cache[key].LastUpdated = DateTime.UtcNow;
            _cache[key].TotalDiscovered += toAdd.Count;

            if (toAdd.Count > 0)
                Console.WriteLine($"[Cache] Added {toAdd.Count} new selectors " +
                                  $"— total: {existing.Count} for {key}");
            else
                Console.WriteLine($"[Cache] No new selectors found — " +
                                  $"total: {existing.Count} for {key}");
        }

        SaveCache();
    }

    public void Invalidate(string url)
    {
        _cache.Remove(NormalizeUrl(url));
        SaveCache();
        Console.WriteLine($"[Cache] Invalidated cache for {url}");
    }

    public int GetCachedCount(string url)
    {
        var key = NormalizeUrl(url);
        return _cache.TryGetValue(key, out var entry) ? entry.Elements.Count : 0;
    }

    private static string NormalizeUrl(string url) => url.ToLower().TrimEnd('/');

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
        public DateTime LastUpdated { get; set; }
        public int TotalDiscovered { get; set; }
    }
}