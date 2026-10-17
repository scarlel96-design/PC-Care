namespace SmartPerformanceDoctor.UpdateEngine;

public sealed partial class UpdateTransaction
{
    /// <summary>Preserve unattested compatibility and require retained history for signed updates.</summary>
    public InstallationTrustDecision InspectStartupTrust(string installRoot)
    {
        try
        {
            var decision = InspectInstallationTrust(installRoot);
            if (!decision.Verified || decision.RequiresReview)
            {
                if (decision.Code == "RESTORED-INSTALLATION-NOT-ATTESTED") return decision;
                if (decision.State is not (InstallationTrustState.PendingTransaction or InstallationTrustState.RecoveryRequired) && IsArchivedLegacyOnly(installRoot))
                    return new(InstallationTrustState.LegacyUnattestedInstallation, false, false, "LEGACY-ARCHIVED-UNATTESTED");
                return decision;
            }
            if (Directory.Exists(_workRoot) && Directory.EnumerateDirectories(_workRoot, "tx-*").Any(directory => TryReadTrustedJournal(directory, installRoot, out var journal, out _) && journal is { Stage: UpdateStage.Committed, SchemaVersion: 2 }))
            {
                var history = InspectInstallationHistory(installRoot);
                if (!history.Verified)
                    return new(history.State == "InstallationHistoryIncomplete" ? InstallationTrustState.InstallationHistoryIncomplete : InstallationTrustState.InstallationHistoryMismatch,
                        false, true, history.Code.StartsWith("INSTALLATION-", StringComparison.Ordinal) ? history.Code : "INSTALLATION-" + history.Code);
                decision = new(InstallationTrustState.SignedHistoryVerified, true, false, "SIGNED-HISTORY-VERIFIED");
            }
            var coverage = InspectExecutableCoverage(installRoot);
            return coverage is null ? decision : new(InstallationTrustState.ManualReviewRequired, false, true, coverage);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException or NullReferenceException)
        { return new(InstallationTrustState.ManualReviewRequired, false, true, "INSTALLATION-REVIEW-REQUIRED"); }
    }

    private bool IsArchivedLegacyOnly(string installRoot)
    {
        if (!Directory.Exists(_workRoot)) return false;
        var directories = Directory.GetDirectories(_workRoot, "tx-*");
        if (directories.Length is 0 or > 4096) return false;
        foreach (var directory in directories)
            if (!TryReadTrustedJournal(directory, installRoot, out var journal, out _) || journal is not { SchemaVersion: 1 } ||
                journal.Stage is not (UpdateStage.Committed or UpdateStage.Restored) || !File.Exists(Path.Combine(directory, "legacy-terminal.receipt"))) return false;
        using var install = WindowsFileBoundary.PinDirectory(installRoot);
        using var pointer = WindowsFileBoundary.OpenRead(_workRoot, "installation-current.json"); VerifyProtectedFile(pointer, _workRoot);
        if (pointer.Stream.Length > 4096) return false;
        using var document = System.Text.Json.JsonDocument.Parse(pointer.Stream); ValidateJsonProperties(document.RootElement);
        var binding = System.Text.Json.JsonSerializer.Deserialize<InstallationPointer>(document.RootElement)!;
        if (binding.InstallIdentity != install.Identity || !Guid.TryParseExact(binding.TransactionId, "N", out _)) return false;
        var transaction = Path.Combine(_workRoot, "tx-" + binding.TransactionId);
        if (!directories.Contains(transaction, StringComparer.OrdinalIgnoreCase)) return false;
        using var pin = WindowsFileBoundary.PinDirectory(transaction);
        if (pin.Identity != binding.TransactionIdentity) return false;
        using var record = WindowsFileBoundary.OpenRead(transaction, "committed-attestation.json"); VerifyProtectedFile(record, transaction);
        if (record.Stream.Length > 2 * 1024 * 1024 || record.Hash() != binding.AttestationHash) return false;
        var saved = System.Text.Json.JsonSerializer.Deserialize<CommittedInstallation>(record.Stream)!;
        return saved.TransactionId == binding.TransactionId && saved.InstallIdentity == install.Identity && SignedPackage.VerifyDeclaration(saved.Manifest, saved.Signature, _policy);
    }

    private string? InspectExecutableCoverage(string installRoot)
    {
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // All retained declarations were authenticated before this coverage check.
        foreach (var directory in Directory.EnumerateDirectories(_workRoot, "tx-*"))
        {
            if (!TryReadTrustedJournal(directory, installRoot, out var journal, out _) || journal is null) return "INSTALLATION-HISTORY-JOURNAL";
            if (journal.Stage == UpdateStage.Committed && journal.SchemaVersion == 2) foreach (var file in journal.Files) declared.Add(Relative(file.Path));
        }
        if (File.Exists(Path.Combine(_workRoot, "installation-enrollment.json")))
        {
            using var file = WindowsFileBoundary.OpenRead(_workRoot, "installation-enrollment.json"); VerifyProtectedFile(file, _workRoot);
            var enrollment = System.Text.Json.JsonSerializer.Deserialize<InstallationEnrollment>(file.Stream)!;
            foreach (var entry in enrollment.Manifest.Files) declared.Add(Relative(entry.Path));
        }
        var pending = new Stack<(string Root, int Depth)>(); pending.Push((installRoot, 0)); var count = 0;
        while (pending.TryPop(out var item))
        {
            if (item.Depth > 32) return "INSTALLATION-COVERAGE-LIMIT";
            using var pin = WindowsFileBoundary.PinDirectory(item.Root);
            foreach (var path in Directory.EnumerateFiles(item.Root))
            {
                if (++count > 10000) return "INSTALLATION-COVERAGE-LIMIT";
                if (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    if (!declared.Contains(Path.GetRelativePath(installRoot, path))) return "INSTALLATION-UNATTESTED-EXECUTABLE";
            }
            foreach (var child in Directory.EnumerateDirectories(item.Root)) pending.Push((child, item.Depth + 1));
            pin.Validate();
        }
        return null;
    }
}
