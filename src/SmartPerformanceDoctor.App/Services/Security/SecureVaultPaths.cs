namespace SmartPerformanceDoctor.App.Services.Security;

public static class SecureVaultPaths
{
    public static string Root
    {
        get
        {
            var testRoot = Environment.GetEnvironmentVariable("SPD_TEST_VAULT_ROOT");
            if (!string.IsNullOrWhiteSpace(testRoot))
            {
                return testRoot;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmartPerformanceDoctor",
                "secure_vault",
                "default");
        }
    }

    public static string MarkerFile => Path.Combine(Root, "vault.svdb");
    public static string KeyEnvelopeFile => Path.Combine(Root, "key_envelope.bin");
    public static string ManifestFile => Path.Combine(Root, "vault_manifest.json.enc");
    public static string DataDirectory => Path.Combine(Root, "data");
    public static string RedundantDataDirectory => Path.Combine(Root, "data", "redundant");
    public static string MetadataDirectory => Path.Combine(Root, "metadata");
    public static string AuditDirectory => Path.Combine(Root, "audit");
    public static string RecoveryDirectory => Path.Combine(Root, "recovery");

    public static string AuditLogFile => Path.Combine(AuditDirectory, "vault_audit.log.enc");
    public static string RecoveryHintFile => Path.Combine(RecoveryDirectory, "recovery_hint.enc");
    public static string RecoveryEnvelopeFile => Path.Combine(RecoveryDirectory, "recovery_envelope.bin");
    public static string RateLimitStateFile => Path.Combine(MetadataDirectory, "rate_limit_state.bin");

    public static bool Exists() => File.Exists(MarkerFile) && File.Exists(KeyEnvelopeFile) && File.Exists(ManifestFile);

    public static void EnsureLayout()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(RedundantDataDirectory);
        Directory.CreateDirectory(MetadataDirectory);
        Directory.CreateDirectory(AuditDirectory);
        Directory.CreateDirectory(RecoveryDirectory);
    }
}