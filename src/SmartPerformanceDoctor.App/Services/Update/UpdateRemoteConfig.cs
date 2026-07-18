namespace SmartPerformanceDoctor.App.Services.Update;

/// <summary>GitHub 및 원격 매니페스트 기본 경로 (PC-Care 릴리즈 저장소).</summary>
public static class UpdateRemoteConfig
{
    public const string RepositoryOwner = "scarlel96-design";
    public const string RepositoryName = "PC-Care";
    public const string RepositorySlug = "scarlel96-design/PC-Care";
    public const string RepositoryUrl = "https://github.com/scarlel96-design/PC-Care";

    public static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{RepositorySlug}/releases/latest";

    /// <summary>prerelease만 있을 때 /releases/latest 는 404 — 목록 API로 조회합니다.</summary>
    public static string ReleasesListApiUrl =>
        $"https://api.github.com/repos/{RepositorySlug}/releases?per_page=30";

    public static string ReleaseManifestRawUrl =>
        $"https://raw.githubusercontent.com/{RepositorySlug}/main/release-manifest.json";

    public static string UpdateChannelRawUrl =>
        $"https://raw.githubusercontent.com/{RepositorySlug}/main/UPDATE_CHANNEL.json";

    public static string ReleaseManifestDownloadUrl(string tagName) =>
        $"{BrowserDownloadUrl(NormalizeTag(tagName), "release-manifest.json")}";

    public static string UpdateChannelDownloadUrl(string tagName) =>
        $"{BrowserDownloadUrl(NormalizeTag(tagName), "UPDATE_CHANNEL.json")}";

    private static string NormalizeTag(string tagName)
    {
        var t = tagName.Trim();
        return t.StartsWith('v') || t.StartsWith('V') ? t : $"v{t}";
    }

    public static string ReleasesPageUrl => $"{RepositoryUrl}/releases";

    public static string BrowserDownloadUrl(string tagName, string fileName)
    {
        var tag = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? tagName : $"v{tagName}";
        return $"{RepositoryUrl}/releases/download/{tag}/{Uri.EscapeDataString(fileName)}";
    }
}