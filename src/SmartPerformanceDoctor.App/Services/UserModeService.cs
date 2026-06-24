using System.Text.Json;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class UserModeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private UserMode _cached = UserMode.Basic;

    public string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartPerformanceDoctor",
        "user_mode.json");

    public UserMode Current => Load();

    public event EventHandler? ModeChanged;

    public UserMode Load()
    {
        if (!File.Exists(SettingsPath))
        {
            _cached = UserMode.Basic;
            return _cached;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            var mode = doc.RootElement.TryGetProperty("mode", out var prop)
                ? prop.GetString()
                : null;
            _cached = Enum.TryParse<UserMode>(mode, true, out var parsed) ? parsed : UserMode.Basic;
        }
        catch
        {
            _cached = UserMode.Basic;
        }

        return _cached;
    }

    public void Save(UserMode mode)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new { mode = mode.ToString() }, JsonOptions));
        _cached = mode;
        ModeChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool Meets(UserMode required) => Current >= required;
}