using System.Text.Json.Serialization;

namespace SmartPerformanceDoctor.App.Models.Update;

public sealed class RemoteReleaseManifestDocument
{
    [JsonPropertyName("product")]
    public string? Product { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("artifacts")]
    public RemoteReleaseArtifacts? Artifacts { get; set; }
}

public sealed class RemoteReleaseArtifacts
{
    [JsonPropertyName("setup")]
    public RemoteReleaseArtifact? Setup { get; set; }

    [JsonPropertyName("update")]
    public RemoteReleaseArtifact? Update { get; set; }
}

public sealed class RemoteReleaseArtifact
{
    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}

public sealed class RemoteUpdateChannelDocument
{
    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("latestVersion")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("minimumSupportedVersion")]
    public string? MinimumSupportedVersion { get; set; }

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("artifacts")]
    public RemoteUpdateChannelArtifacts? Artifacts { get; set; }
}

public sealed class RemoteUpdateChannelArtifacts
{
    [JsonPropertyName("setup")]
    public RemoteReleaseArtifact? Setup { get; set; }

    [JsonPropertyName("update")]
    public RemoteReleaseArtifact? Update { get; set; }
}

public sealed class GitHubReleaseApiDocument
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAsset>? Assets { get; set; }

    [JsonPropertyName("draft")]
    public bool? Draft { get; set; }

    [JsonPropertyName("prerelease")]
    public bool? Prerelease { get; set; }
}

public sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>GitHub release asset digest, e.g. sha256:abc…</summary>
    [JsonPropertyName("digest")]
    public string? Digest { get; set; }
}

public sealed record RemoteUpdateCheckResult(
    bool Success,
    string LatestVersion,
    string? UpdateFileName,
    string? UpdateDownloadUrl,
    string? ExpectedSha256,
    string Channel,
    string Message,
    IReadOnlyList<string> ReleaseNotesLines);

public sealed record RemoteUpdateDownloadResult(
    bool Success,
    string? LocalPath,
    string Message);

/// <summary>GitHub에서 패키지를 받는 동안 표시할 간결한 전송 상태입니다.</summary>
public sealed record RemoteUpdateDownloadProgress(
    long DownloadedBytes,
    long? TotalBytes,
    string Phase);