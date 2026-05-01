namespace IchScraper.Models;

public record Guideline
{
    public string GuidelineCode { get; init; }
    public string Title { get; init; }
    public string Category { get; init; }
    public string Step { get; init; }
    public string Status { get; init; }
    public DateTime Dated { get; init; }
    public string Summary { get; init; }
    public string SourceUrl { get; init; }
}