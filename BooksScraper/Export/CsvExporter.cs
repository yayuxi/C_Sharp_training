using System.Globalization;
using BooksScraper.Models;
using CsvHelper;

namespace BooksScraper.Export;

public class CsvExporter {
        public static void Export(List<Book> books, string filePath) {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(books);
        }
    
}