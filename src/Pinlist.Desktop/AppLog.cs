using System.IO;

namespace Pinlist.Desktop;

internal static class AppLog
{
    private const long MaxLogBytes = 512 * 1024;
    private static readonly object Sync = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Pinlist",
        "pinlist.log");

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxLogBytes)
                    File.WriteAllText(LogPath, string.Empty);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Diagnostics must never affect the widget.
        }
    }
}
