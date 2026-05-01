using CsvHelper;
using IchScraper.Models;
using System.Globalization;

namespace IchScraper.Export;

public static class CsvExporter
{
    public static void ExportGuidelines(List<Guideline> guidelines, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(guidelines);
        Console.WriteLine($"Saved {guidelines.Count} guidelines to {filePath}");
    }

    public static void ExportDocuments(List<GuidelineDocument> documents, string filePath)
    {
        using var writer = new StreamWriter(filePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(documents);
        Console.WriteLine($"Saved {documents.Count} documents to {filePath}");
    }
}