namespace SmartPerformanceDoctor.Contracts.Models.Installation;

public static class InstallFeatureIds
{
    public const string CoreDiagnostics = "core-diagnostics";
    public const string ReportAudit = "report-audit";
    public const string ProgramIntegrity = "program-integrity";
    public const string ConfigManager = "config-manager";
    public const string UpdateManifest = "update-manifest";

    public const string SystemCare = "system-care";
    public const string DriverAudioRepair = "driver-audio-repair";
    public const string SecureVault = "secure-vault";
    public const string ProfessionalSecureDelete = "professional-secure-delete";
    public const string RegistryDoctor = "registry-doctor";
    public const string DiskDoctor = "disk-doctor";
    public const string PrivacyCleaner = "privacy-cleaner";
    public const string JunkCleaner = "junk-cleaner";
    public const string ShortcutRepair = "shortcut-repair";
    public const string InternetAcceleration = "internet-acceleration";
    public const string VulnerabilityFix = "vulnerability-fix";
    public const string DeepScanIntelligence = "deep-scan-intelligence";
    public const string KnowledgePack = "knowledge-pack";
    public const string PortableTools = "portable-tools";

    public static IReadOnlyList<string> Required { get; } =
    [
        CoreDiagnostics,
        ReportAudit,
        ProgramIntegrity,
        ConfigManager,
        UpdateManifest
    ];

    public static IReadOnlyList<string> Optional { get; } =
    [
        SystemCare,
        DriverAudioRepair,
        SecureVault,
        ProfessionalSecureDelete,
        RegistryDoctor,
        DiskDoctor,
        PrivacyCleaner,
        JunkCleaner,
        ShortcutRepair,
        InternetAcceleration,
        VulnerabilityFix,
        DeepScanIntelligence,
        KnowledgePack,
        PortableTools
    ];

    public static IReadOnlyList<string> All { get; } = Required.Concat(Optional).ToArray();
}