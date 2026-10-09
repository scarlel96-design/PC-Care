namespace SmartPerformanceDoctor.App.Models.Update;

public enum UpdatePublisherTrustLevel
{
    Unverified,
    ContentVerifiedOnly,
    PublisherVerified,
    VerificationFailed
}

public sealed record UpdatePackageInspection(
    bool IsValid,
    string PackagePath,
    UpdateManifestDocument? Manifest,
    string Status,
    string Message,
    bool CanApply,
    bool RequiresRestart,
    bool PackageIntegrityVerified = false,
    UpdatePublisherTrustLevel PublisherTrust = UpdatePublisherTrustLevel.Unverified);
