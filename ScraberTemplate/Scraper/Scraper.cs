using Microsoft.Playwright;
using ScraberTemplate.Helpers;
using ScraberTemplate.Models;

namespace ScraberTemplate.Scraper;

public class Scraper
{
    private readonly IPage _page;

    public Scraper(IPage page)
    {
        _page = page;
    }

    public async Task<List<Guideline>> ScrapeGuidelines()
    {
        // Placeholder: Implement scraping logic for guidelines/index items
        // For template purposes, this scrapes basic book titles as guidelines
        await RetryHelper.ExecuteAsync(
            () => _page.WaitForSelectorAsync(".product_pod"),
            "Waiting for product elements to load"); //.product_pod is the area of information, when finding the information on another site,
                                                            //there might be need for going deeper in the website to find the right element to scrape

        var elements = await _page.QuerySelectorAllAsync(".product_pod h3 a");
        var guidelines = new List<Guideline>();

        foreach (var element in elements)
        {
            // Change the following elements to the corresponding information on the target website, find it by looking at the HTML of the website
            var title = await element.InnerTextAsync();
            var url = await element.GetAttributeAsync("href");
            guidelines.Add(new Guideline
            {
                GuidelineCode = Guid.NewGuid().ToString(),
                Title = title.Trim(),
                Category = "Template Category",
                Step = "1",
                Status = "Active",
                Dated = DateTime.Now,
                Summary = $"Template guideline: {title}",
                SourceUrl = url ?? ""
            });
        }

        return guidelines;
    }

    public async Task<List<GuidelineDocument>> ScrapeGuidelineDocuments()
    {
        // Placeholder: Implement scraping logic for documents
        // For template purposes, this scrapes book images as documents
        await RetryHelper.ExecuteAsync(
            () => _page.WaitForSelectorAsync(".product_pod img"),
            "Waiting for image elements to load");

        var elements = await _page.QuerySelectorAllAsync(".product_pod img");
        var documents = new List<GuidelineDocument>();

        foreach (var element in elements)
        {
            // Change the following elements to the corresponding information on the target website, find it by looking at the HTML of the website
            var title = await element.GetAttributeAsync("alt");
            var url = await element.GetAttributeAsync("src");
            documents.Add(new GuidelineDocument
            {
                GuidelineCode = Guid.NewGuid().ToString(),
                DocumentTitle = title ?? "Untitled",
                DocumentUrl = (url != null && url.StartsWith("http")) ? url : $"https://books.toscrape.com{url}",
                DocumentType = "Image",
                FileFormat = "jpg"
            });
        }

        return documents;
    }
}