namespace SmartPerformanceDoctor.Aegis;

public static class AegisMirrorPaths
{
    private static string? _mirrorRootOverride;
    private static string? _cachedRoot;
    private static bool _usingUserFallback;

    public static bool UsesTestOverride => _mirrorRootOverride is not null;

    public static bool UsingUserFallback => _usingUserFallback;

    public static void SetMirrorRootOverride(string? mirrorRoot)
    {
        _mirrorRootOverride = string.IsNullOrWhiteSpace(mirrorRoot)
            ? null
            : Path.GetFullPath(mirrorRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _cachedRoot = null;
        _usingUserFallback = false;
    }

    public static string Root
    {
        get
        {
            if (_mirrorRootOverride is not null)
            {
                return _mirrorRootOverride;
            }

            if (_cachedRoot is not null)
            {
                return _cachedRoot;
            }

            _cachedRoot = ResolveWritableRoot(out _usingUserFallback);
            return _cachedRoot;
        }
    }

    public static string LegacyRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            AegisProduct.LegacyProgramDataFolder,
            "AegisMirror");

    public static string ManifestFile => Path.Combine(Root, "recovery.manifest.json");
    public static string ManifestSignatureFile => Path.Combine(Root, "recovery.manifest.sig");
    public static string CapsuleFile => Path.Combine(Root, "recovery.capsule");
    public static string PolicyFile => Path.Combine(Root, "recovery_policy.json");
    public static string LastKnownGoodDirectory => Path.Combine(Root, "last_known_good");
    public static string StagingDirectory => Path.Combine(Root, "staging");
    public static string QuarantineDirectory => Path.Combine(Root, "quarantine");
    public static string LogsDirectory => Path.Combine(Root, "logs");
    public static string AuditLogFile => Path.Combine(LogsDirectory, "aegis_audit.log");
    public static string OfflineDirectory => Path.Combine(Root, "offline");
    public static string ActiveSlotDirectory => Path.Combine(Root, "active");
    public static string BackupSlotDirectory => Path.Combine(Root, "backup");
    public static string LaunchStateFile => Path.Combine(Root, "launch_state.json");
    public static string WatchdogStateFile => Path.Combine(Root, "watchdog_state.json");

    public static string ProgramDataRoot
    {
        get
        {
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var candidates = new[]
            {
                Path.Combine(programData, AegisProduct.ProgramDataFolder),
                Path.Combine(programData, AegisProduct.LegacyProgramDataFolder),
                Path.Combine(programData, AegisProduct.LegacyProgramDataFolder2)
            };
            return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
        }
    }

    public static string ServiceConfigFile => Path.Combine(ProgramDataRoot, "aegis_service.json");

    public static void ResetRootCache()
    {
        if (_mirrorRootOverride is null)
        {
            _cachedRoot = null;
            _usingUserFallback = false;
        }
    }

    public static bool EnsureLayout()
    {
        if (_mirrorRootOverride is not null)
        {
            return TryCreateLayout(_mirrorRootOverride);
        }

        if (_cachedRoot is not null && TryCreateLayout(_cachedRoot))
        {
            return true;
        }

        _cachedRoot = ResolveWritableRoot(out _usingUserFallback);
        return TryCreateLayout(_cachedRoot);
    }

    private static string ResolveWritableRoot(out bool usingUserFallback)
    {
        usingUserFallback = false;
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var machineCandidates = new List<string>
        {
            Path.Combine(programData, AegisProduct.ProgramDataFolder, "AegisMirror")
        };

        if (Directory.Exists(LegacyRoot))
        {
            machineCandidates.Add(LegacyRoot);
        }

        foreach (var candidate in machineCandidates)
        {
            if (CanUseMirrorRoot(candidate))
            {
                return candidate;
            }
        }

        usingUserFallback = true;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AegisProduct.ProgramDataFolder,
            "AegisMirror");
    }

    private static bool CanUseMirrorRoot(string root)
    {
        try
        {
            Directory.CreateDirectory(root);
            var probe = Path.Combine(root, $".write_probe_{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateLayout(string root)
    {
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "last_known_good"));
            Directory.CreateDirectory(Path.Combine(root, "staging"));
            Directory.CreateDirectory(Path.Combine(root, "quarantine"));
            Directory.CreateDirectory(Path.Combine(root, "logs"));
            Directory.CreateDirectory(Path.Combine(root, "offline"));
            Directory.CreateDirectory(Path.Combine(root, "active"));
            Directory.CreateDirectory(Path.Combine(root, "backup"));
            return true;
        }
        catch
        {
            if (_mirrorRootOverride is null)
            {
                _cachedRoot = null;
            }

            return false;
        }
    }
}