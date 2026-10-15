using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App.Services.Update;

public sealed class UpdateInstallerService
{
    private readonly UpdatePackageInspector _legacyInspector = new();
    private static string? _pendingSignedPackage;

    public Task<UpdatePackageInspection> InspectAsync(string packagePath,
        IProgress<UpdateProgressReport>? progress = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => InspectCore(packagePath, progress, cancellationToken), cancellationToken);

    public UpdatePackageInspection Inspect(string packagePath) => InspectCore(packagePath, null, default);

    private UpdatePackageInspection InspectCore(string packagePath, IProgress<UpdateProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        var recovery = UpdateRecoveryCoordinator.Inspect();
        if (recovery.State is UpdateRecoveryState.RecoveryAvailable or UpdateRecoveryState.RecoveryRequired or
            UpdateRecoveryState.RecoveryInProgress or UpdateRecoveryState.RecoveryFailed)
            return new UpdatePackageInspection(false, packagePath, null, "RECOVERY-REQUIRED",
                "이전 업데이트 복구가 필요합니다. 복구를 완료한 뒤 새 업데이트를 시작하세요.",
                false, false, false, UpdatePublisherTrustLevel.Unverified);
        if (!IsV2(packagePath)) return _legacyInspector.Inspect(packagePath, AppInfo.Version, progress, cancellationToken);
        var result = SignedPackage.Verify(packagePath, AppInfo.Version, SmartPerformanceDoctor.UpdateEngine.BuildPublisherTrust.Current,
            cancellationToken: cancellationToken);
        var legacy = result.Manifest is null ? null : new UpdateManifestDocument
        {
            Format = result.Manifest.Format,
            Product = result.Manifest.Product,
            Channel = result.Manifest.Channel,
            FromVersion = result.Manifest.FromVersion,
            ToVersion = result.Manifest.ToVersion,
            MinimumSupportedVersion = result.Manifest.MinimumSupportedVersion,
            CreatedAt = result.Manifest.CreatedAt.ToString("O"),
            Files = result.Manifest.Files.Select(f => new UpdateManifestFileEntry
            {
                Path = f.Path, Sha256 = f.Sha256
            }).ToList()
        };
        var trust = result.Trust switch
        {
            PublisherVerification.PublisherVerified => UpdatePublisherTrustLevel.PublisherVerified,
            PublisherVerification.ContentVerifiedOnly => UpdatePublisherTrustLevel.ContentVerifiedOnly,
            PublisherVerification.VerificationFailed => UpdatePublisherTrustLevel.VerificationFailed,
            _ => UpdatePublisherTrustLevel.Unverified
        };
        return new UpdatePackageInspection(result.Trust is PublisherVerification.PublisherVerified or
            PublisherVerification.ContentVerifiedOnly, packagePath, legacy, result.Code, result.Message,
            result.CanApply, true, result.Trust is PublisherVerification.PublisherVerified or
            PublisherVerification.ContentVerifiedOnly, trust);
    }

    private static bool IsV2(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = archive.GetEntry(SignedPackage.ManifestName);
            if (entry is null || entry.Length > 1024 * 1024) return false;
            using var reader = new StreamReader(entry.Open());
            using var document = JsonDocument.Parse(reader.ReadToEnd());
            return document.RootElement.TryGetProperty("Format", out var format) &&
                   format.GetString() == "spd-update-v2";
        }
        catch { return false; }
    }

    public void InvalidateInspectionCache() => _pendingSignedPackage = null;

    public async Task<UpdateApplyResult> ApplyAsync(UpdatePackageInspection inspection,
        IProgress<UpdateProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = progress;
        var current = await InspectAsync(inspection.PackagePath,progress,cancellationToken).ConfigureAwait(false);
        if (!current.CanApply || current.Manifest is null)
        {
            var message = current.PublisherTrust == UpdatePublisherTrustLevel.ContentVerifiedOnly
                ? "발행자 인증이 없는 업데이트는 앱에서 적용할 수 없습니다. 공식 설치 파일로 직접 업데이트해 주세요."
                : current.Message;
            return new UpdateApplyResult(false, AppInfo.Version, "", message, false, 0, 0);
        }
        var route=BootstrapDispatch.InspectRoute(UpdatePaths.AppInstallDirectory);
        if(route.State is not (BootstrapTransitionState.BootstrapNotRequired or BootstrapTransitionState.BootstrapTransitionRequired))
            return new UpdateApplyResult(false,AppInfo.Version,"",route.Code+" · "+route.Message,false,0,0);
        cancellationToken.ThrowIfCancellationRequested();
        _pendingSignedPackage = current.PackagePath;
        return new UpdateApplyResult(true, AppInfo.Version, current.Manifest.ToVersion,
            route.Message+" Windows 관리자 승인 후 적용을 시작합니다.", true, 0, 0);
    }

    public static bool EnsurePendingApplyScript(string stagingDir, string toVersion)
    {
        _ = stagingDir;
        _ = toVersion;
        return false;
    }

    public static BootstrapDispatchResult LaunchPendingRestartDetailed()
    {
        var package=_pendingSignedPackage;
        if(package is null)return BootstrapDispatch.Result(BootstrapTransitionState.SignedPackageRejected);
        var result=BootstrapDispatch.Launch(UpdatePaths.AppInstallDirectory,package,AppInfo.Version,Environment.ProcessId);
        if(result.ProcessLaunched)_pendingSignedPackage=null;
        return result;
    }
    public static bool LaunchPendingRestart()=>LaunchPendingRestartDetailed().ProcessLaunched;
}
