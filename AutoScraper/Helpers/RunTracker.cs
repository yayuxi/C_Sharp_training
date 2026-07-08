using System.Text.Json;

namespace ScraperTemplate.Helpers;

public class RunTracker
{
    private readonly string _trackingFile = "run_history.json";
    private List<RunRecord> _history;

    public RunTracker()
    {
        _history = LoadHistory();
    }

    public void RecordRun(int guidelines, int documents)
    {
        _history.Add(new RunRecord
        {
            RunNumber = _history.Count + 1,
            Timestamp = DateTime.Now,
            GuidelinesFound = guidelines,
            DocumentsFound = documents
        });
        SaveHistory();
    }

    public void PrintSummary()
    {
        if (_history.Count == 0)
        {
            Console.WriteLine("[Tracker] No previous runs recorded.");
            return;
        }

        Console.WriteLine($"\n{new string('=', 60)}");
        Console.WriteLine("RUN HISTORY");
        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"{"Run",-6} {"Timestamp",-22} {"Guidelines",-14} {"Documents",-12} {"Change"}");
        Console.WriteLine(new string('-', 60));

        for (int i = 0; i < _history.Count; i++)
        {
            var run = _history[i];
            var prev = i > 0 ? _history[i - 1] : null;

            var guidelineChange = prev == null ? "" :
                run.GuidelinesFound > prev.GuidelinesFound
                    ? $"+{run.GuidelinesFound - prev.GuidelinesFound} G"
                    : run.GuidelinesFound < prev.GuidelinesFound
                        ? $"-{prev.GuidelinesFound - run.GuidelinesFound} G"
                        : "= G";

            var docChange = prev == null ? "" :
                run.DocumentsFound > prev.DocumentsFound
                    ? $" +{run.DocumentsFound - prev.DocumentsFound} D"
                    : run.DocumentsFound < prev.DocumentsFound
                        ? $" -{prev.DocumentsFound - run.DocumentsFound} D"
                        : " = D";

            var isCurrent = i == _history.Count - 1 ? " ◄ current" : "";

            Console.WriteLine($"{run.RunNumber,-6} " +
                              $"{run.Timestamp:dd/MM/yyyy HH:mm:ss,-22} " +
                              $"{run.GuidelinesFound,-14} " +
                              $"{run.DocumentsFound,-12} " +
                              $"{guidelineChange}{docChange}{isCurrent}");
        }

        var first = _history[0];
        var latest = _history[^1];
        Console.WriteLine(new string('-', 60));
        Console.WriteLine($"{"Total improvement:",-28} " +
                          $"{latest.GuidelinesFound - first.GuidelinesFound:+#;-#;0} guidelines, " +
                          $"{latest.DocumentsFound - first.DocumentsFound:+#;-#;0} documents");
        Console.WriteLine($"{"Runs recorded:",-28} {_history.Count}");
        Console.WriteLine(new string('=', 60));
    }

    private List<RunRecord> LoadHistory()
    {
        if (!File.Exists(_trackingFile)) return new();
        try
        {
            var json = File.ReadAllText(_trackingFile);
            return JsonSerializer.Deserialize<List<RunRecord>>(json) ?? new();
        }
        catch { return new(); }
    }

    private void SaveHistory()
    {
        File.WriteAllText(_trackingFile,
            JsonSerializer.Serialize(_history,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private class RunRecord
    {
        public int RunNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public int GuidelinesFound { get; set; }
        public int DocumentsFound { get; set; }
    }
}