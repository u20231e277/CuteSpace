namespace CuteSpace.Services;

public static class SafeLog
{
    private static readonly object Gate = new();

    public static void Write(string area, string message)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CuteSpace",
                "logs");
            Directory.CreateDirectory(folder);

            var line = $"{DateTimeOffset.Now:u} [{area}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(folder, "cutespace.log"), line);
            }
        }
        catch
        {
            // Logging should never become the reason the app closes.
        }
    }
}
