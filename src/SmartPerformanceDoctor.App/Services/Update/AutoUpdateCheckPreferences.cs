using System.Text.Json;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class AutoUpdateCheckPreferences
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromHours(6);

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCCare",
        "settings",
        "auto_update.json");

    public bool Enabled { get; set; } = true;

    public DateTimeOffset? LastCheckUtc { get; set; }

    public string? DismissedVersion { get; set; }

    public TimeSpan CheckInterval { get; set; } = DefaultInterval;

    public static AutoUpdateCheckPreferences Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AutoUpdateCheckPreferences();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AutoUpdateCheckPreferences>(json) ?? new AutoUpdateCheckPreferences();
        }
        catch
        {
            return new AutoUpdateCheckPreferences();
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

    public bool ShouldCheckNow()
    {
        if (LastCheckUtc is null)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - LastCheckUtc.Value >= CheckInterval;
    }

    public void RecordCheck() => LastCheckUtc = DateTimeOffset.UtcNow;

    public bool IsDismissed(string version) =>
        !string.IsNullOrWhiteSpace(DismissedVersion)
        && string.Equals(DismissedVersion, version, StringComparison.OrdinalIgnoreCase);

    public void DismissVersion(string version) => DismissedVersion = version;
}