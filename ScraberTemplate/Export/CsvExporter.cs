namespace ScraberTemplate.Export;

public class CsvExporter {
        public static void Export<T>(List<T> data, string filePath) {
            using var writer = new StreamWriter(filePath);
            using var csv = new CsvHelper.CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
            csv.WriteRecords(data);
        }
}