using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Update;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdatePackageInspector
{
    public const string ManifestEntryName = "update.manifest.json";
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
            var manifestEntry = archive.GetEntry(ManifestEntryName)
                                 ?? archive.Entries.FirstOrDefault(x =>
                                     x.FullName.Equals(ManifestEntryName, StringComparison.OrdinalIgnoreCase));

            if (manifestEntry is null)
            {
                return Invalid(packagePath, "NO-MANIFEST", "패키지 안에 update.manifest.json 이 없습니다.");
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

            var packageIntegrityVerified = false;
            if (!string.IsNullOrWhiteSpace(manifest.PackageSha256))
            {
                reporter.Report(70, 5, StepCount, "검사", "지문 검증", $"파일 목록 지문 확인 · {manifest.Files.Count}개 항목");
                var fingerprint = ComputeManifestFingerprint(manifest);
                if (!string.Equals(fingerprint, manifest.PackageSha256, StringComparison.OrdinalIgnoreCase))
                {
                    reporter.Report(70, 5, StepCount, "검사", "지문 재검증", "목록 지문 불일치 · ZIP 내용 해시로 재확인 중…");
                    if (!TryVerifyArchiveFileHashes(archive, manifest, cancellationToken, out var hashError))
                    {
                        return Invalid(
                            packagePath,
                            "CHECKSUM-FAIL",
                            $"패키지 체크섬이 일치하지 않습니다. {hashError}");
                    }
                }

                packageIntegrityVerified = true;
            }
            else
            {
                reporter.Report(70, 5, StepCount, "검사", "지문 검증", "패키지 지문 없음 · 매니페스트 구조만 확인");
            }

            if (manifest.Files.Count == 0)
            {
                return Invalid(packagePath, "NO-FILES", "적용할 파일 목록이 비어 있습니다.");
            }

            reporter.Report(100, StepCount, StepCount, "검사", "준비 완료", $"적용 가능 · 파일 {manifest.Files.Count}개");
            return new UpdatePackageInspection(
                true,
                packagePath,
                manifest,
                "READY",
                $"{manifest.FromVersion} → {manifest.ToVersion} 업데이트를 적용할 수 있습니다.",
                true,
                manifest.RequiresRestart,
                packageIntegrityVerified);
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

    private static bool TryVerifyArchiveFileHashes(
        ZipArchive archive,
        UpdateManifestDocument manifest,
        CancellationToken cancellationToken,
        out string error)
    {
        error = "";
        var checkedCount = 0;

        foreach (var entry in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.Path) || string.IsNullOrWhiteSpace(entry.Sha256))
            {
                error = "매니페스트 파일 항목이 비어 있습니다.";
                return false;
            }

            var zipEntry = FindZipEntry(archive, entry.Path);
            if (zipEntry is null)
            {
                error = $"ZIP에 파일이 없습니다: {entry.Path}";
                return false;
            }

            using var stream = zipEntry.Open();
            var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!string.Equals(actual, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                error = $"파일 해시 불일치: {entry.Path}";
                return false;
            }

            checkedCount++;
        }

        if (checkedCount == 0)
        {
            error = "검증할 파일이 없습니다.";
            return false;
        }

        return true;
    }

    private static string NormalizeZipEntryPath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    /// <summary>
    /// PowerShell Compress-Archive often stores entries with backslashes while the
    /// manifest uses forward slashes (payload/foo.dll). Match both styles.
    /// </summary>
    private static ZipArchiveEntry? FindZipEntry(ZipArchive archive, string path)
    {
        var forward = NormalizeZipEntryPath(path);
        var backslash = forward.Replace('/', '\\');

        var entry = archive.GetEntry(forward)
                    ?? archive.GetEntry(backslash)
                    ?? archive.GetEntry(path);
        if (entry is not null)
        {
            return entry;
        }

        return archive.Entries.FirstOrDefault(e =>
        {
            var full = NormalizeZipEntryPath(e.FullName);
            return string.Equals(full, forward, StringComparison.OrdinalIgnoreCase);
        });
    }
}