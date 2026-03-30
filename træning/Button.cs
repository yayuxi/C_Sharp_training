namespace træning;

public class Button {
    public event EventHandler? Click;

    public void ButtonClick() {
        Click?.Invoke(this, EventArgs.Empty);
    }
}

public class Logger {
    public Logger(Button button) {
        button.Click += ButtonOnClick;
    }

    private void ButtonOnClick(object? sender, EventArgs e) {
        LogTextToFile("Button is clicked!");
    }

    static void LogTextToFile(string text) {
        using (StreamWriter writer = new(
                   File.Open("C:\\Users\\Bruger 1\\RiderProjects\\C_Sharp_training\\træning\\log.txt", 
                    FileMode.Append, FileAccess.Write, FileShare.Write)))
        {
            writer.WriteLine($"{DateTime.Now}: {text}");
        }
    }
}

public class Print {
    public Print(Button button) {
        button.Click += ButtonOnClick;
    }

    private void ButtonOnClick(object? sender, EventArgs e) {
        Console.WriteLine("Button is clicked!");
    }

}