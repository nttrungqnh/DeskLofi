namespace DeskLofi.Services;

public static class LoggerService
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "Logs", "desklifi.log");
    private const long MaxBytes = 1024 * 1024;
    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", exception is null ? message : $"{message}: {exception.GetType().Name}: {exception.Message}");
    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > MaxBytes)
                {
                    var old = LogPath + ".1"; if (File.Exists(old)) File.Delete(old); File.Move(LogPath, old);
                }
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
