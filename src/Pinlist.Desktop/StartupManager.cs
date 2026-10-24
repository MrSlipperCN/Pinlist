using Microsoft.Win32;
using System.IO;
using System.Security;

namespace Pinlist.Desktop;

internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Pinlist";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executable = Path.Combine(AppContext.BaseDirectory, "Pinlist.Desktop.exe");
        key.SetValue(ValueName, $"\"{executable}\"");
    }
}
