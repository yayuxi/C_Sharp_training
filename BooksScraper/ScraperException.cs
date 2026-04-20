namespace BooksScraper;

public class ScraperException : Exception
{
    public ScraperException(string message) : base(message) { }
    public ScraperException(string message, Exception inner) : base(message, inner) { }
}

public class PageLoadException : ScraperException
{
    public string Url { get; }
    public PageLoadException(string url) 
        : base($"Failed to load page after 3 attempts: {url}") 
    {
        Url = url;
    }
}

public class ElementNotFoundException : ScraperException
{
    public ElementNotFoundException(string selector) 
        : base($"Required element not found: {selector}") { }
}