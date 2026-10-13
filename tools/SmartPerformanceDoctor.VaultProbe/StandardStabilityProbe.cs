using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

internal static class StandardStabilityProbe
{
    private const string Password = "WO15-Synthetic-Only!";
    private const string NewPassword = "WO15-New-Synthetic-Only!";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    internal static int Run(string[] args)
    {
        if (args[0] == "--wo16-reconcile") return Reconcile(args[1], args.Length > 2 ? args[2] : null);
        if (args[0] == "--wo16-generation-child") return GenerationChild(args[1], args[2], args[3]);
        Check(args.Length == 2 && args[0] == "--wo16", "arguments");
        var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var parent = Path.Combine(Path.GetTempPath(), "wo16-private-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        var receipts = new List<object>();
        foreach (var stage in new[] { "TrustedBeforeConsumption", "TrustedAfterConsumption", "AnchorBeforeReplace:2" })
        {
            var root = TrustedRecoveryProbe.Seed(parent, "receipt-" + stage.Replace(':', '-'));
            var operation = Guid.NewGuid(); File.WriteAllText(Path.Combine(root, "operation.txt"), operation.ToString());
            TrustedRecoveryProbe.Kill(root, "trusted", stage);
            var before = TrustedRecoveryProbe.Hash(Path.Combine(root, "vault", "recovery", "recovery_slots.v6.json"));
            var result = RunChild("--wo16-reconcile", root);
            Check(result.Exit == 0 && result.Output.Contains(stage == "TrustedBeforeConsumption" ? "InterruptedReview" : "ReceiptAcknowledged"), "receipt:" + stage);
            var again = RunChild("--wo16-reconcile", root); Check(again == result, "idempotent-query");
            Check(before == TrustedRecoveryProbe.Hash(Path.Combine(root, "vault", "recovery", "recovery_slots.v6.json")), "no-second-consumption");
            receipts.Add(new { stage, status = "PASS", result = result.Output, childExit = result.Exit,
                repeatedQuery = again.Output, recoveryStoreUnchanged = true, callerAcknowledgementExactlyOnce = "NOT PROVEN", powerLoss = "NOT TESTED" });
        }
        foreach (var stage in new[] { "ReconciliationConsumptionVerified", "ReconciliationBeforeAnchorCommit", "ReconciliationAfterAnchorCommit",
                     "ReconciliationBeforeResponse", "TrustedBeforeAcknowledgement", "TrustedAfterAcknowledgement" })
        {
            var root = TrustedRecoveryProbe.Seed(parent, "reconcile-" + stage);
            File.WriteAllText(Path.Combine(root, "operation.txt"), Guid.NewGuid().ToString());
            TrustedRecoveryProbe.Kill(root, "trusted", "TrustedAfterConsumption"); File.Delete(Path.Combine(root, "barrier.signal"));
            KillChild(root, "--wo16-reconcile", stage);
            var result = RunChild("--wo16-reconcile", root);
            Check(result.Exit == 0 && result.Output.Contains("ReceiptAcknowledged"), "reconcile-crash:" + stage);
            receipts.Add(new { stage, status = "PASS", result = result.Output, childExit = result.Exit, termination = "separate-process-kill", powerLoss = "NOT TESTED" });
        }
        Write(output, "RECOVERY_RECEIPT_RESULT.json", receipts);
        var generations = new List<object>();
        foreach (var action in new[] { "import", "password" })
        foreach (var stage in new[] { "GenerationBeforeAnchorCommit", "GenerationAfterAnchorCommit" })
        {
            var root = TrustedRecoveryProbe.Seed(parent, action + "-" + stage);
            using (var child = TrustedRecoveryProbe.Launch("--wo16-generation-child", root, action, stage)) WaitAndKill(child, root, stage);
            using var vault = new LabVaultService(Path.Combine(root, "vault"));
            Check(vault.UnlockReadOnly(action == "password" ? NewPassword : Password).Success, "generation-open");
            var evidence = vault.CaptureFreshnessEvidence(); var posture = TrustedRecoveryProbe.Store(root).Verify(evidence).Posture;
            Check(posture == (stage == "GenerationBeforeAnchorCommit" ? RecoveryPosture.AuthenticButStale : RecoveryPosture.FreshnessVerified), "generation-state");
            Check(vault.VerifyAllContent(CancellationToken.None).Count == (action == "import" ? 2 : 1), "generation-content");
            generations.Add(new { action, stage, status = "PASS", posture = posture.ToString(), generation = evidence.Generation,
                authenticatedContent = "PASS", externalStatePending = stage == "GenerationBeforeAnchorCommit", powerLoss = "NOT TESTED" });
        }
        Write(output, "GENERATION_RESULT.json", generations);
        var legacy = new List<object>();
        foreach (var stage in new[] { "LegacyPasswordHeadersWritten", "LegacyPasswordSlotsWritten" })
        {
            var root = TrustedRecoveryProbe.Seed(parent, "legacy-" + stage, true);
            var source = Path.Combine(root, "vault"); var backup = Path.Combine(root, "authenticated-header.json");
            File.Copy(Path.Combine(source, "vault.header.json"), backup);
            TrustedRecoveryProbe.Kill(root, "legacy", stage);
            var old = RunChild("--wo15-read", root, "old"); var newer = RunChild("--wo15-read", root, "new");
            Check(old.Output == "PRESERVED_BUT_LOCKED" && newer.Output == "PRESERVED_BUT_LOCKED", "legacy-baseline");
            var before = TrustedRecoveryProbe.Hashes(source);
            var result = LabV5Transition.ExecuteSynthetic(source, Path.Combine(root, "work-salvage"), Password, true,
                salvageReadOnly: true, authenticatedHeaderBackup: backup);
            Check(result.Status == "ReadOnlyRecovery", "legacy-recovery");
            var extracted = Path.Combine(root, "work-salvage", "recovered", "seed.bin");
            var expected = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3, 4, 5 }));
            Check(TrustedRecoveryProbe.Hash(extracted) == expected, "legacy-bytes");
            var after = TrustedRecoveryProbe.Hashes(source); Check(before.Count == after.Count && before.All(p => after.GetValueOrDefault(p.Key) == p.Value), "legacy-source-preserved");
            legacy.Add(new { stage, status = "ReadOnlyRecovery", previousPasswordNormalUnlock = old.Output, newPasswordNormalUnlock = newer.Output,
                authenticatedHeaderBackupRequired = true, sourceUnchanged = true, originalSha256 = expected, extractedSha256 = TrustedRecoveryProbe.Hash(extracted),
                bytes = 5, sourceHashes = before, productionMigration = false });
        }
        Write(output, "V5_RECOVERY_RESULT.json", legacy);
        Write(output, "FILE_INTEGRITY_RESULT.json", new { status = "PASS", receiptReopenRuns = receipts.Count, v5ReadOnlyRecoveries = legacy.Count,
            originalBytes = "VERIFIED", sourceDeletion = false, generationAuthenticatedContent = "VERIFIED", fixtures = "SYNTHETIC_ONLY" });
        File.WriteAllText(Path.Combine(output, "private-fixture-location.txt"), parent);
        Console.WriteLine($"RECEIPT_RECONCILIATION={receipts.Count};GENERATION_CRASH={generations.Count};V5_READONLY={legacy.Count};SHA256=PASS;POWER_LOSS=NOT_TESTED"); return 0;
    }
    private static int Reconcile(string root, string? stage)
    {
        LabLocalTrustedState.ValidateFixture(root);
        void Fault(string point) { if (point == stage) { File.WriteAllText(Path.Combine(root, "barrier.signal"), point); Thread.Sleep(Timeout.Infinite); } }
        using var vault = new LabVaultService(Path.Combine(root, "vault")); Check(vault.UnlockReadOnly(Password).Success, "reconcile-open"); vault.FaultObserver = Fault;
        var store = TrustedRecoveryProbe.Store(root); store.FaultObserver = Fault;
        var id = Guid.Parse(File.ReadAllText(Path.Combine(root, "operation.txt")));
        RecoveryReceipt? receipt;
        try { receipt = vault.ReconcileTrustedRecovery(id, File.ReadAllText(Path.Combine(root, "private-code.txt")), store); }
        catch (InvalidDataException)
        {
            Check(store.Query(id)?.State == RecoveryReceiptState.Interrupted && store.Verify(vault.CaptureFreshnessEvidence()).Posture == RecoveryPosture.FreshnessVerified,
                "unexplained-review"); VerifySeed(vault, root); Console.WriteLine("InterruptedReview;BYTES_VERIFIED;NO_CONSUMPTION"); return 0;
        }
        if (receipt.State == RecoveryReceiptState.ReceiptPending) receipt = vault.AcknowledgeTrustedRecovery(id, store);
        VerifySeed(vault, root); Console.WriteLine(receipt.State + ";BYTES_VERIFIED;NO_SECOND_CONSUMPTION"); return 0;
    }
    private static void VerifySeed(LabVaultService vault, string root)
    {
        var output = Path.Combine(root, "export-" + Guid.NewGuid().ToString("N"));
        Check(vault.ExportEntry(vault.List().Single(e => e.DisplayName == "seed.bin").EntryId, output, true).Success, "export");
        Check(File.ReadAllBytes(Path.Combine(output, "seed.bin")).AsSpan().SequenceEqual(new byte[] { 1, 2, 3, 4, 5 }), "bytes");
    }
    private static int GenerationChild(string root, string action, string stage)
    {
        LabLocalTrustedState.ValidateFixture(root); using var vault = new LabVaultService(Path.Combine(root, "vault"));
        Check(vault.Unlock(Password).Success, "generation-unlock");
        vault.FaultObserver = p => { if (p == stage) { File.WriteAllText(Path.Combine(root, "barrier.signal"), p); Thread.Sleep(Timeout.Infinite); } };
        if (action == "import") vault.ImportWithTrustedAnchor("next.bin", [6, 7], TrustedRecoveryProbe.Store(root));
        else if (action == "password") vault.ChangePasswordWithTrustedAnchor(Password, NewPassword, TrustedRecoveryProbe.Store(root));
        else throw new InvalidOperationException("action"); return 3;
    }
    private static (int Exit, string Output) RunChild(params string[] args)
    { using var child = TrustedRecoveryProbe.Launch(args); var text = child.StandardOutput.ReadToEnd(); var error = child.StandardError.ReadToEnd(); child.WaitForExit(); Check(child.ExitCode == 0, "child:" + error); return (child.ExitCode, text.Trim()); }
    private static void KillChild(string root, string verb, string stage)
    { using var child = TrustedRecoveryProbe.Launch(verb, root, stage); WaitAndKill(child, root, stage); }
    private static void WaitAndKill(Process child, string root, string stage)
    { var timer = Stopwatch.StartNew(); while (!File.Exists(Path.Combine(root, "barrier.signal")) && !child.HasExited && timer.Elapsed.TotalSeconds < 120) Thread.Sleep(20);
        Check(File.Exists(Path.Combine(root, "barrier.signal")), "barrier:" + stage + ":" + (child.HasExited ? child.StandardError.ReadToEnd() : "timeout")); child.Kill(true); child.WaitForExit(); }
    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Json));
}
