using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Update;

namespace SmartPerformanceDoctor.App.Services;

public static class AppVersionService
{
    private const string FallbackVersion = AppInfo.BuildVersion;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string GetInstalledVersion()
    {
        // The running assembly can be the previous version while a deferred
        // updater has already written the new files. Keep both sources so the
        // update checker never falls back to a stale historic version.
        var resolved = ResolveAssemblyVersion();
        var stored = ReadStoredVersion();
        var current = string.IsNullOrWhiteSpace(resolved) ? FallbackVersion : resolved;
        if (!string.IsNullOrWhiteSpace(stored))
        {
            current = PreferNewer(current, stored);
        }

        return PreferNewer(current, FallbackVersion);
    }

    private static string? ReadStoredVersion()
    {
        if (!File.Exists(UpdatePaths.InstalledVersionFile))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(UpdatePaths.InstalledVersionFile));
            return doc.RootElement.TryGetProperty("version", out var versionNode)
                ? Normalize(versionNode.GetString() ?? "")
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveAssemblyVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            return Normalize(informational);
        }

        try
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(asm.Location).ProductVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion))
            {
                return Normalize(fileVersion);
            }
        }
        catch
        {
            // Fall through.
        }

        return null;
    }

    private static string PreferNewer(string a, string b)
    {
        if (UpdateVersionComparer.TryParse(a, out var va) && UpdateVersionComparer.TryParse(b, out var vb))
        {
            return va >= vb ? a : b;
        }

        return a;
    }

    public static VersionVerificationResult VerifyInstalledVersion(string? expectedVersion)
    {
        var actual = GetInstalledVersion();
        var mainDll = Path.Combine(UpdatePaths.AppInstallDirectory, "SmartPerformanceDoctor.dll");
        var fileVersion = File.Exists(mainDll)
            ? Normalize(FileVersionInfo.GetVersionInfo(mainDll).ProductVersion ?? "")
            : "";

        if (string.IsNullOrWhiteSpace(expectedVersion))
        {
            return new VersionVerificationResult(true, actual, fileVersion, expectedVersion ?? "", "기대 버전 없음");
        }

        var runtimeOk = UpdateVersionComparer.Compare(actual, expectedVersion) >= 0;
        var fileOk = !string.IsNullOrWhiteSpace(fileVersion)
            && UpdateVersionComparer.Compare(fileVersion, expectedVersion) >= 0;
        var storedVersion = ReadStoredVersion();
        var storedOk = !string.IsNullOrWhiteSpace(storedVersion)
            && UpdateVersionComparer.Compare(storedVersion, expectedVersion) >= 0;

        // Deferred updates run outside this process. In that window the loaded
        // assembly is intentionally old, so verify the on-disk DLL/state rather
        // than rejecting a successful replacement because of the old runtime.
        var ok = fileOk || storedOk || (string.IsNullOrWhiteSpace(fileVersion) && runtimeOk);
        var details = ok
            ? $"설치 확인됨 · 실행 버전 {actual} · DLL {fileVersion}"
            : $"버전 불일치 · 실행 {actual} · DLL {fileVersion} · 기대 {expectedVersion}";

        return new VersionVerificationResult(ok, actual, fileVersion, expectedVersion, details);
    }

    public static void WriteInstalledVersion(string version, string source)
    {
        UpdatePaths.EnsureLayout();
        var payload = new
        {
            version = Normalize(version),
            source,
            verifiedAt = DateTimeOffset.Now.ToString("o"),
            installDir = UpdatePaths.AppInstallDirectory
        };
        File.WriteAllText(UpdatePaths.InstalledVersionFile, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static string Normalize(string version)
    {
        var trimmed = version.Trim();
        var plus = trimmed.IndexOf('+');
        if (plus >= 0)
        {
            trimmed = trimmed[..plus];
        }

        return trimmed;
    }
}

public sealed record VersionVerificationResult(
    bool Success,
    string AssemblyVersion,
    string FileVersion,
    string ExpectedVersion,
    string Details);