using System.Text.Json.Serialization;

namespace SmartPerformanceDoctor.App.Models.Update;

public sealed class UpdateManifestDocument
{
    public const string FormatId = "spd-update-v1";

    [JsonPropertyName("format")]
    public string Format { get; set; } = FormatId;

    [JsonPropertyName("product")]
    public string Product { get; set; } = AppInfo.ProductNameEnglish;

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "stable";

    [JsonPropertyName("fromVersion")]
    public string FromVersion { get; set; } = "";

    [JsonPropertyName("toVersion")]
    public string ToVersion { get; set; } = "";

    [JsonPropertyName("minimumSupportedVersion")]
    public string MinimumSupportedVersion { get; set; } = "44.0.0";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = "";

    [JsonPropertyName("changes")]
    public List<string> Changes { get; set; } = new();

    [JsonPropertyName("requiresRestart")]
    public bool RequiresRestart { get; set; } = true;

    [JsonPropertyName("packageSha256")]
    public string PackageSha256 { get; set; } = "";

    [JsonPropertyName("files")]
    public List<UpdateManifestFileEntry> Files { get; set; } = new();
}

public sealed class UpdateManifestFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";
}