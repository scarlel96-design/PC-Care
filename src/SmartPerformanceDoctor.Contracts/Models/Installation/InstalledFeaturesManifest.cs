using System.Text.Json.Serialization;

namespace SmartPerformanceDoctor.Contracts.Models.Installation;

public sealed class InstalledFeaturesManifest
{
    [JsonPropertyName("product")]
    public string Product { get; set; } = "PC 케어 프로";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("installMode")]
    public string InstallMode { get; set; } = "custom";

    [JsonPropertyName("installedAt")]
    public string InstalledAt { get; set; } = "";

    [JsonPropertyName("features")]
    public Dictionary<string, bool> Features { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled(string featureId) =>
        Features.TryGetValue(featureId, out var enabled) && enabled;
}