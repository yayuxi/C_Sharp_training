namespace IchScraper.Models;

public record GuidelineDocument
{
    public string GuidelineCode { get; init; }
    public string DocumentTitle { get; init; }
    public string DocumentUrl { get; init; }
    public string DocumentType { get; init; }
    public string FileFormat { get; init; } 
}