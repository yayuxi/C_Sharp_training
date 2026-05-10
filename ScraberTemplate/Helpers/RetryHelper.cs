namespace ScraberTemplate.Helpers;

public class RetryHelper {
    private const int MaxRetries = 3;
    private const int DelayMs = 3000;

    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, string context = "")
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                Console.WriteLine($"[Retry {attempt}/{MaxRetries}] {context} — {ex.Message}");
                await Task.Delay(DelayMs);
            }
        }
        // Final attempt — let it throw naturally
        return await action();
    }

    public static async Task ExecuteAsync(Func<Task> action, string context = "")
    {
        await ExecuteAsync<bool>(async () => { await action(); return true; }, context);
    }
}