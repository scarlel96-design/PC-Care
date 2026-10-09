using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Update;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdatePackageInspector
{
    public const string ManifestEntryName = "update.manifest.json";
    public const long MaxEntryBytes = 512L * 1024 * 1024;
    public const long MaxPackageBytes = 2L * 1024 * 1024 * 1024;
    public const int MaxEntries = 10_000;
    private const long MaxManifestBytes = 2L * 1024 * 1024;
    private const int StepCount = 5;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<UpdatePackageInspection> InspectAsync(
        string packagePath,
        string currentVersion,
        IProgress<UpdateProgressReport>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Inspect(packagePath, currentVersion, progress, cancellationToken), cancellationToken);

    public UpdatePackageInspection Inspect(
        string packagePath,
        string currentVersion,
        IProgress<UpdateProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var reporter = new UpdateProgressReporter(progress);

        if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
        {
            return Invalid(packagePath, "NO-FILE", "업데이트 파일을 찾을 수 없습니다.");
        }

        var extension = Path.GetExtension(packagePath).ToLowerInvariant();
        if (extension is not ".spdup" and not ".zip")
        {
            return Invalid(packagePath, "BAD-EXTENSION", "지원 형식: .spdup 또는 .zip");
        }

        try
        {
            reporter.Report(5, 1, StepCount, "검사", "파일 확인", $"패키지 파일 열기 · {Path.GetFileName(packagePath)}");
            cancellationToken.ThrowIfCancellationRequested();

            var fileSizeMb = new FileInfo(packagePath).Length / 1024.0 / 1024.0;
            reporter.Report(15, 2, StepCount, "검사", "ZIP 열기", $"압축 패키지 읽기 · {fileSizeMb:F1} MB");

            using var archive = ZipFile.OpenRead(packagePath);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaxEntries)
            {
                return Invalid(packagePath, "ENTRY-LIMIT", "ZIP 항목 수가 허용 범위를 벗어났습니다.");
            }

            var manifestEntry = archive.GetEntry(ManifestEntryName)
                                 ?? archive.Entries.FirstOrDefault(x =>
                                     x.FullName.Equals(ManifestEntryName, StringComparison.OrdinalIgnoreCase));

            if (manifestEntry is null)
            {
                return Invalid(packagePath, "NO-MANIFEST", "패키지 안에 update.manifest.json 이 없습니다.");
            }
            if (manifestEntry.Length > MaxManifestBytes)
            {
                return Invalid(packagePath, "MANIFEST-LIMIT", "매니페스트 크기 제한을 초과했습니다.");
            }

            reporter.Report(30, 3, StepCount, "검사", "매니페스트 읽기", "update.manifest.json 파싱 중…");
            using var stream = manifestEntry.Open();
            using var reader = new StreamReader(stream);
            var manifest = JsonSerializer.Deserialize<UpdateManifestDocument>(reader.ReadToEnd(), JsonOptions);
            if (manifest is null)
            {
                return Invalid(packagePath, "MANIFEST-PARSE", "매니페스트를 읽을 수 없습니다.");
            }

            if (!string.Equals(manifest.Format, UpdateManifestDocument.FormatId, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid(packagePath, "BAD-FORMAT", $"지원하지 않는 패키지 형식: {manifest.Format}");
            }

            if (string.IsNullOrWhiteSpace(manifest.ToVersion))
            {
                return Invalid(packagePath, "NO-VERSION", "대상 버전(toVersion)이 없습니다.");
            }

            reporter.Report(50, 4, StepCount, "검사", "버전 확인", $"{manifest.FromVersion} → {manifest.ToVersion} · 최소 {manifest.MinimumSupportedVersion}");
            if (!UpdateVersionComparer.IsSupported(currentVersion, manifest.MinimumSupportedVersion))
            {
                return Invalid(
                    packagePath,
                    "UNSUPPORTED",
                    $"현재 버전({currentVersion})은 이 업데이트의 최소 지원 버전({manifest.MinimumSupportedVersion})보다 낮습니다.");
            }

            if (!UpdateVersionComparer.IsNewer(manifest.ToVersion, currentVersion))
            {
                return new UpdatePackageInspection(
                    true,
                    packagePath,
                    manifest,
                    "ALREADY-INSTALLED",
                    $"현재 버전({currentVersion})과 같거나 더 높습니다. 패키지 버전({manifest.ToVersion})보다 높은 업데이트 파일이 필요합니다.",
                    false,
                    manifest.RequiresRestart);
            }

            if (manifest.Files.Count == 0)
            {
                return Invalid(packagePath, "NO-FILES", "적용할 파일 목록이 비어 있습니다.");
            }

            if (!string.IsNullOrWhiteSpace(manifest.PackageSha256))
            {
                reporter.Report(70, 5, StepCount, "검사", "지문 검증", $"파일 목록 지문 확인 · {manifest.Files.Count}개 항목");
                var fingerprint = ComputeManifestFingerprint(manifest);
                if (!string.Equals(fingerprint, manifest.PackageSha256, StringComparison.OrdinalIgnoreCase))
                {
                    // Older packages may use a different list-fingerprint recipe.
                    // The authoritative gate below is the actual ZIP content hash for every file.
                    reporter.Report(70, 5, StepCount, "검사", "지문 재검증", "목록 지문 불일치 · 실제 파일 해시를 검증 중…");
                }
            }

            reporter.Report(75, 5, StepCount, "검사", "파일 검증", "ZIP의 실제 파일과 SHA-256을 검증 중…");
            if (!TryVerifyArchiveFileHashes(archive, manifest, cancellationToken, out var hashError))
            {
                return Invalid(packagePath, "CHECKSUM-FAIL", hashError);
            }

            reporter.Report(100, StepCount, StepCount, "검사", "내용 검증 완료", $"발행자 인증 미완료 · 파일 {manifest.Files.Count}개");
            return new UpdatePackageInspection(
                true,
                packagePath,
                manifest,
                "PUBLISHER-UNVERIFIED",
                $"{manifest.FromVersion} → {manifest.ToVersion} 파일 내용은 확인됐으나 발행자 인증은 완료되지 않았습니다.",
                false,
                manifest.RequiresRestart,
                true,
                UpdatePublisherTrustLevel.ContentVerifiedOnly);
        }
        catch (Exception ex)
        {
            return Invalid(packagePath, "INSPECT-FAIL", $"패키지 검사 실패: {ex.Message}");
        }
    }

    private static UpdatePackageInspection Invalid(string packagePath, string status, string message) =>
        new(false, packagePath, null, status, message, false, true);

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string ComputeManifestFingerprint(UpdateManifestDocument manifest)
    {
        // Deterministic: lowercased path + Ordinal sort (must match create-update-package.ps1).
        var joined = string.Join('|', manifest.Files
            .Select(f => $"{f.Path.ToLowerInvariant()}:{f.Sha256.ToLowerInvariant()}")
            .OrderBy(s => s, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(joined))).ToLowerInvariant();
    }

    public static bool TryGetSafePayloadRelativePath(string path, out string relative)
    {
        relative = "";
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['\0', ':']) >= 0
            || path.StartsWith('/') || path.StartsWith('\\'))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("payload/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = normalized.Split('/');
        if (parts.Length < 2 || parts.Any(IsUnsafeWindowsSegment))
        {
            return false;
        }

        relative = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(1));
        return !string.IsNullOrWhiteSpace(relative);
    }

    private static bool IsUnsafeWindowsSegment(string part)
    {
        if (string.IsNullOrWhiteSpace(part) || part is "." or ".."
            || part.EndsWith(' ') || part.EndsWith('.')
            || part.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0
            || part.Any(char.IsControl))
        {
            return true;
        }

        var stem = part.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                                     || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && stem[3] is >= '1' and <= '9');
    }

    internal static bool TryVerifyArchiveFileHashes(
        ZipArchive archive,
        UpdateManifestDocument manifest,
        CancellationToken cancellationToken,
        out string error)
    {
        error = "";
        var expected = new Dictionary<string, UpdateManifestFileEntry>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;

        foreach (var file in manifest.Files)
        {
            if (!TryGetSafePayloadRelativePath(file.Path, out _)
                || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit)
                || !expected.TryAdd(NormalizeZipEntryPath(file.Path), file))
            {
                error = $"매니페스트 경로·해시가 잘못됐거나 중복됐습니다: {file.Path}";
                return false;
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zipEntry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = NormalizeZipEntryPath(zipEntry.FullName);
            if (path.Equals(ManifestEntryName, StringComparison.OrdinalIgnoreCase))
            {
                if (!seen.Add(ManifestEntryName))
                {
                    error = "매니페스트가 중복됐습니다.";
                    return false;
                }
                continue;
            }

            if (path.EndsWith('/'))
            {
                var marker = path.TrimEnd('/') + "/placeholder";
                if (!TryGetSafePayloadRelativePath(marker, out _))
                {
                    error = $"안전하지 않은 ZIP 디렉터리: {zipEntry.FullName}";
                    return false;
                }
                continue;
            }

            if (!TryGetSafePayloadRelativePath(zipEntry.FullName, out _)
                || !expected.TryGetValue(path, out var file)
                || !seen.Add(path))
            {
                error = $"ZIP 파일이 매니페스트와 다르거나 중복됐습니다: {zipEntry.FullName}";
                return false;
            }

            if (zipEntry.Length > MaxEntryBytes || totalBytes > MaxPackageBytes - zipEntry.Length)
            {
                error = "압축 해제 크기 제한을 초과했습니다.";
                return false;
            }
            totalBytes += zipEntry.Length;

            using var stream = zipEntry.Open();
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[128 * 1024];
            long readBytes = 0;
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                readBytes += count;
                if (readBytes > MaxEntryBytes || readBytes > zipEntry.Length)
                {
                    error = $"파일 크기 제한 또는 ZIP 길이 불일치: {path}";
                    return false;
                }
                sha.AppendData(buffer, 0, count);
            }
            if (readBytes != zipEntry.Length
                || !string.Equals(Convert.ToHexString(sha.GetHashAndReset()), file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                error = $"파일 해시 또는 길이 불일치: {path}";
                return false;
            }
        }

        if (seen.Count != expected.Count + 1)
        {
            error = "매니페스트에 선언한 파일 중 ZIP에 없는 항목이 있습니다.";
            return false;
        }

        return true;
    }

    internal static string NormalizeZipEntryPath(string path) => path.Replace('\\', '/');

}
