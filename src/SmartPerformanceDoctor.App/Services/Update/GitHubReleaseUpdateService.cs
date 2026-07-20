using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmartPerformanceDoctor.App.Models.Update;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class GitHubReleaseUpdateService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex Sha256BodyRegex = new(
        @"update-sha256:\s*([a-fA-F0-9]{64})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _http;

    public GitHubReleaseUpdateService()
    {
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"PCCare/{AppInfo.BuildVersion}");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<RemoteUpdateCheckResult> CheckLatestAsync(CancellationToken cancellationToken = default)
    {
        var current = AppInfo.Version;

        var manifest = await TryCheckReleaseManifestAsync(current, cancellationToken).ConfigureAwait(false);
        if (manifest is not null)
        {
            return await EnrichReleaseNotesAsync(manifest, cancellationToken).ConfigureAwait(false);
        }

        var channel = await TryCheckUpdateChannelAsync(current, cancellationToken).ConfigureAwait(false);
        if (channel is not null)
        {
            return channel;
        }

        var releaseManifest = await TryCheckReleaseManifestFromPublishedReleaseAsync(current, cancellationToken)
            .ConfigureAwait(false);
        if (releaseManifest is not null)
        {
            return releaseManifest;
        }

        return await TryCheckGitHubApiAsync(current, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RemoteUpdateDownloadResult> DownloadUpdatePackageAsync(
        RemoteUpdateCheckResult check,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!check.Success)
        {
            return new RemoteUpdateDownloadResult(false, null, check.Message);
        }

        if (string.IsNullOrWhiteSpace(check.UpdateDownloadUrl))
        {
            return new RemoteUpdateDownloadResult(false, null, "다운로드 URL이 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(check.UpdateFileName))
        {
            return new RemoteUpdateDownloadResult(false, null, "업데이트 파일 이름이 없습니다.");
        }

        try
        {
            UpdatePaths.EnsureLayout();
            var destination = Path.Combine(UpdatePaths.Inbox, check.UpdateFileName);
            progress?.Report($"다운로드 시작: {check.UpdateFileName}");

            using (var response = await _http.GetAsync(check.UpdateDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var file = File.Create(destination);
                await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report("SHA256 검증 중…");
            var actualHash = await ComputeSha256Async(destination, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(check.ExpectedSha256)
                && !string.Equals(actualHash, check.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(destination);
                return new RemoteUpdateDownloadResult(
                    false,
                    null,
                    $"SHA256 불일치 — 기대 {check.ExpectedSha256}, 실제 {actualHash}");
            }

            progress?.Report($"다운로드 완료: {destination}");
            return new RemoteUpdateDownloadResult(true, destination, "GitHub 업데이트 패키지를 받았습니다.");
        }
        catch (Exception ex)
        {
            return new RemoteUpdateDownloadResult(false, null, $"다운로드 오류: {ex.Message}");
        }
    }

    private async Task<RemoteUpdateCheckResult?> TryCheckReleaseManifestAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await _http.GetStringAsync(UpdateRemoteConfig.ReleaseManifestRawUrl, cancellationToken)
                .ConfigureAwait(false);
            var doc = JsonSerializer.Deserialize<RemoteReleaseManifestDocument>(json, JsonOptions);
            if (doc is null || string.IsNullOrWhiteSpace(doc.Version))
            {
                return null;
            }

            var update = doc.Artifacts?.Update;
            var fileName = update?.File;
            var sha256 = update?.Sha256;
            var downloadUrl = string.IsNullOrWhiteSpace(fileName)
                ? null
                : UpdateRemoteConfig.BrowserDownloadUrl(doc.Version, fileName);

            return BuildCheckResult(
                currentVersion,
                doc.Version,
                fileName,
                downloadUrl,
                sha256,
                AppInfo.Channel,
                "release-manifest.json",
                NormalizeReleaseNotes(doc.ReleaseNotes));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>main 브랜치에 매니페스트가 없을 때, 최신 릴리즈 태그에 올린 JSON 자산을 조회합니다.</summary>
    private async Task<RemoteUpdateCheckResult?> TryCheckReleaseManifestFromPublishedReleaseAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var release = await FetchNewestReleaseFromListAsync(cancellationToken).ConfigureAwait(false);
            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return null;
            }

            var tag = release.TagName;
            foreach (var url in new[]
                     {
                         UpdateRemoteConfig.ReleaseManifestDownloadUrl(tag),
                         UpdateRemoteConfig.UpdateChannelDownloadUrl(tag)
                     })
            {
                try
                {
                    var json = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
                    var manifest = JsonSerializer.Deserialize<RemoteReleaseManifestDocument>(json, JsonOptions);
                    if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.Version))
                    {
                        var update = manifest.Artifacts?.Update;
                        return BuildCheckResult(
                            currentVersion,
                            manifest.Version,
                            update?.File,
                            string.IsNullOrWhiteSpace(update?.File)
                                ? null
                                : UpdateRemoteConfig.BrowserDownloadUrl(manifest.Version, update.File),
                            update?.Sha256,
                            AppInfo.Channel,
                            $"release-manifest (tag {tag})",
                            NormalizeReleaseNotes(manifest.ReleaseNotes));
                    }

                    var channelDoc = JsonSerializer.Deserialize<RemoteUpdateChannelDocument>(json, JsonOptions);
                    if (channelDoc is not null && !string.IsNullOrWhiteSpace(channelDoc.LatestVersion))
                    {
                        var update = channelDoc.Artifacts?.Update;
                        var notes = NormalizeReleaseNotes(channelDoc.ReleaseNotes);

                        return BuildCheckResult(
                            currentVersion,
                            channelDoc.LatestVersion,
                            update?.File,
                            string.IsNullOrWhiteSpace(update?.File)
                                ? null
                                : UpdateRemoteConfig.BrowserDownloadUrl(channelDoc.LatestVersion, update.File),
                            update?.Sha256,
                            channelDoc.Channel ?? AppInfo.Channel,
                            $"UPDATE_CHANNEL (tag {tag})",
                            notes);
                    }
                }
                catch
                {
                    // try next URL
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private async Task<RemoteUpdateCheckResult?> TryCheckUpdateChannelAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await _http.GetStringAsync(UpdateRemoteConfig.UpdateChannelRawUrl, cancellationToken)
                .ConfigureAwait(false);
            var doc = JsonSerializer.Deserialize<RemoteUpdateChannelDocument>(json, JsonOptions);
            if (doc is null || string.IsNullOrWhiteSpace(doc.LatestVersion))
            {
                return null;
            }

            var update = doc.Artifacts?.Update;
            var fileName = update?.File;
            var sha256 = update?.Sha256;
            var downloadUrl = string.IsNullOrWhiteSpace(fileName)
                ? null
                : UpdateRemoteConfig.BrowserDownloadUrl(doc.LatestVersion, fileName);

            var notes = NormalizeReleaseNotes(doc.ReleaseNotes);

            return BuildCheckResult(
                currentVersion,
                doc.LatestVersion,
                fileName,
                downloadUrl,
                sha256,
                doc.Channel ?? AppInfo.Channel,
                "UPDATE_CHANNEL.json",
                notes);
        }
        catch
        {
            return null;
        }
    }

    private async Task<RemoteUpdateCheckResult> TryCheckGitHubApiAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            var release = await FetchLatestReleaseDocumentAsync(cancellationToken).ConfigureAwait(false)
                ?? await FetchNewestReleaseFromListAsync(cancellationToken).ConfigureAwait(false);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
            {
                return new RemoteUpdateCheckResult(
                    false,
                    currentVersion,
                    null,
                    null,
                    null,
                    AppInfo.Channel,
                    "GitHub 릴리즈를 찾지 못했습니다. 저장소에 게시된 릴리즈(또는 prerelease)와 .spdup 자산을 확인하세요.",
                    Array.Empty<string>());
            }

            var source = string.IsNullOrWhiteSpace(release.Name) ? "GitHub 릴리즈" : $"GitHub · {release.Name}";
            return MapReleaseToCheckResult(currentVersion, release, source);
        }
        catch (Exception ex)
        {
            var detail = ex.Message;
            if (ex.Message.Contains("404", StringComparison.Ordinal))
            {
                detail = "릴리즈 API 404 — main에 UPDATE_CHANNEL.json이 없거나 latest가 비어 있을 수 있습니다. 릴리즈 목록 조회도 실패했습니다.";
            }

            return new RemoteUpdateCheckResult(
                false,
                currentVersion,
                null,
                null,
                null,
                AppInfo.Channel,
                $"GitHub 확인 오류: {detail}",
                Array.Empty<string>());
        }
    }

    private async Task<GitHubReleaseApiDocument?> FetchLatestReleaseDocumentAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync(UpdateRemoteConfig.LatestReleaseApiUrl, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GitHubReleaseApiDocument>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task<GitHubReleaseApiDocument?> FetchNewestReleaseFromListAsync(CancellationToken cancellationToken)
    {
        var json = await _http.GetStringAsync(UpdateRemoteConfig.ReleasesListApiUrl, cancellationToken)
            .ConfigureAwait(false);
        var releases = JsonSerializer.Deserialize<List<GitHubReleaseApiDocument>>(json, JsonOptions);
        return SelectHighestVersionRelease(releases);
    }

    private static GitHubReleaseApiDocument? SelectHighestVersionRelease(IEnumerable<GitHubReleaseApiDocument>? releases)
    {
        GitHubReleaseApiDocument? best = null;
        string? bestVersion = null;

        foreach (var release in releases ?? Array.Empty<GitHubReleaseApiDocument>())
        {
            if (release.Draft == true || string.IsNullOrWhiteSpace(release.TagName))
            {
                continue;
            }

            var version = release.TagName.Trim().TrimStart('v', 'V');
            if (best is null || UpdateVersionComparer.IsNewer(version, bestVersion!))
            {
                best = release;
                bestVersion = version;
            }
        }

        return best;
    }

    private static RemoteUpdateCheckResult MapReleaseToCheckResult(
        string currentVersion,
        GitHubReleaseApiDocument release,
        string source)
    {
        var version = release.TagName!.Trim().TrimStart('v', 'V');
        var updateAsset = release.Assets?
            .FirstOrDefault(a => a.Name?.EndsWith(".spdup", StringComparison.OrdinalIgnoreCase) == true)
            ?? release.Assets?.FirstOrDefault(a => a.Name?.Contains("Update", StringComparison.OrdinalIgnoreCase) == true);

        var fileName = updateAsset?.Name;
        var downloadUrl = updateAsset?.BrowserDownloadUrl;
        var sha256 = ParseSha256FromBody(release.Body) ?? ParseSha256FromAssetDigest(updateAsset?.Digest);
        var notes = NormalizeReleaseNotes(release.Body);

        return BuildCheckResult(
            currentVersion,
            version,
            fileName,
            downloadUrl,
            sha256,
            AppInfo.Channel,
            source,
            notes);
    }

    private static RemoteUpdateCheckResult BuildCheckResult(
        string currentVersion,
        string latestVersion,
        string? fileName,
        string? downloadUrl,
        string? sha256,
        string channel,
        string source,
        IReadOnlyList<string> releaseNotes)
    {
        if (!UpdateVersionComparer.IsNewer(latestVersion, currentVersion))
        {
            return new RemoteUpdateCheckResult(
                true,
                latestVersion,
                fileName,
                downloadUrl,
                sha256,
                channel,
                $"최신 버전입니다 ({currentVersion}). [{source}]",
                releaseNotes);
        }

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(downloadUrl))
        {
            return new RemoteUpdateCheckResult(
                false,
                latestVersion,
                fileName,
                downloadUrl,
                sha256,
                channel,
                $"새 버전 {latestVersion}이 있으나 업데이트 파일 정보가 없습니다. [{source}]",
                releaseNotes);
        }

        var shaNote = string.IsNullOrWhiteSpace(sha256) ? " (SHA256 미지정)" : "";
        return new RemoteUpdateCheckResult(
            true,
            latestVersion,
            fileName,
            downloadUrl,
            sha256,
            channel,
            $"업데이트 가능: {currentVersion} → {latestVersion}{shaNote} [{source}]",
            releaseNotes);
    }

    private static string? ParseSha256FromBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var match = Sha256BodyRegex.Match(body);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    private static string? ParseSha256FromAssetDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return null;
        }

        const string prefix = "sha256:";
        if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return digest[prefix.Length..].Trim().ToLowerInvariant();
    }

    private async Task<RemoteUpdateCheckResult> EnrichReleaseNotesAsync(
        RemoteUpdateCheckResult result,
        CancellationToken cancellationToken)
    {
        if (result.ReleaseNotesLines.Count > 0)
        {
            return result;
        }

        // Older release manifests did not contain release notes. Preserve their
        // artifact metadata, but supplement notes from the channel or GitHub body.
        var channel = await TryCheckUpdateChannelAsync(result.LatestVersion, cancellationToken).ConfigureAwait(false);
        if (channel is not null
            && string.Equals(channel.LatestVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase)
            && channel.ReleaseNotesLines.Count > 0)
        {
            return result with { ReleaseNotesLines = channel.ReleaseNotesLines };
        }

        try
        {
            var release = await FetchNewestReleaseFromListAsync(cancellationToken).ConfigureAwait(false);
            var releaseVersion = release?.TagName?.Trim().TrimStart('v', 'V');
            if (release is not null
                && string.Equals(releaseVersion, result.LatestVersion, StringComparison.OrdinalIgnoreCase))
            {
                var notes = NormalizeReleaseNotes(release.Body);
                if (notes.Count > 0)
                {
                    return result with { ReleaseNotesLines = notes };
                }
            }
        }
        catch
        {
            // Update detection remains usable even when optional note enrichment fails.
        }

        return result;
    }

    internal static IReadOnlyList<string> NormalizeReleaseNotes(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<string>();
        }

        return body
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('#'))
            .Where(line => !line.StartsWith("update-sha256:", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("---"))
            .Where(line => !line.Contains("**배포 파일**", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("- 설치:", StringComparison.OrdinalIgnoreCase))
            .Where(line => !line.StartsWith("- 업데이트:", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.StartsWith("- ", StringComparison.Ordinal) ? line[2..].Trim() : line)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(12)
            .ToArray();
    }
    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose() => _http.Dispose();
}