namespace ScraperTemplate.Helpers;

public static class StringExtensions
{
    /// <summary>Returns the fallback value if the string is null or whitespace.</summary>
    public static string IfEmpty(this string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}