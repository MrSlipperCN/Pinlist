using System.Text.Json;
using System.IO;

namespace Pinlist.Desktop;

public sealed record AppSettings(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsCollapsed,
    bool HideDuringFullscreen,
    bool StartWithWindows,
    bool GlobalHotkeyEnabled,
    bool AlwaysOnTop,
    string? Language)
{
    public static AppSettings Default { get; } = new(
        double.NaN,
        double.NaN,
        360,
        560,
        true,
        true,
        false,
        true,
        false,
        null);
}

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public AppSettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return AppSettings.Default;
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.Default;
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(nameof(AppSettings.GlobalHotkeyEnabled), out _))
            {
                settings = settings with { GlobalHotkeyEnabled = true };
            }
            if (!document.RootElement.TryGetProperty(nameof(AppSettings.AlwaysOnTop), out _))
            {
                settings = settings with { AlwaysOnTop = false };
            }
            if (!document.RootElement.TryGetProperty(nameof(AppSettings.Language), out _))
            {
                settings = settings with { Language = null };
            }
            return settings;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
        catch (IOException)
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var temporaryPath = _settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, _settingsPath, true);
    }
}
