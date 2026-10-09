using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Services.Update;

/// <summary>
/// The legacy v1 update format binds content to a manifest, but does not bind
/// either to an authenticated publisher. No in-app install path may consume it.
/// </summary>
public sealed class UpdateInstallerService
{
    private readonly UpdatePackageInspector _inspector = new();

    public Task<UpdatePackageInspection> InspectAsync(
        string packagePath,
        IProgress<UpdateProgressReport>? progress = null,
        CancellationToken cancellationToken = default) =>
        _inspector.InspectAsync(packagePath, AppInfo.Version, progress, cancellationToken);

    public UpdatePackageInspection Inspect(string packagePath) =>
        _inspector.Inspect(packagePath, AppInfo.Version);

    public void InvalidateInspectionCache() { }

    public Task<UpdateApplyResult> ApplyAsync(
        UpdatePackageInspection inspection,
        IProgress<UpdateProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = progress;

        // Reinspect the package rather than trusting the UI's cached result.
        // Even a content-valid v1 package has no authenticated publisher binding.
        var current = _inspector.Inspect(inspection.PackagePath, AppInfo.Version,
            cancellationToken: cancellationToken);
        var message = current.PublisherTrust == UpdatePublisherTrustLevel.ContentVerifiedOnly
            ? "발행자 인증이 없는 업데이트는 앱에서 적용할 수 없습니다. 공식 설치 파일로 직접 업데이트해 주세요."
            : current.Message;
        return Task.FromResult(new UpdateApplyResult(false, AppInfo.Version, "", message,
            false, 0, 0));
    }

    // Existing callers may query a pending restart created by an older build.
    // A legacy pending state is not an authorization to run an elevated script.
    public static bool EnsurePendingApplyScript(string stagingDir, string toVersion)
    {
        _ = stagingDir;
        _ = toVersion;
        return false;
    }

    public static bool LaunchPendingRestart() => false;
}
