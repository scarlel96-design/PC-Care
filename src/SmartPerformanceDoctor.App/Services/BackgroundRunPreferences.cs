using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services;

public sealed class BackgroundRunPreferences
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCCare",
        "settings",
        "background.json");

    public bool RunAtWindowsStartup { get; set; }
    public bool RunInBackgroundOnClose { get; set; }

    public static BackgroundRunPreferences Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new BackgroundRunPreferences();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<BackgroundRunPreferences>(json) ?? new BackgroundRunPreferences();
        }
        catch
        {
            return new BackgroundRunPreferences();
        }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}