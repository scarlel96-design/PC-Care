using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

internal static class RecoveryCrashProbe
{
    private const string Password = "WO12-Synthetic-Only!";
    private static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
    private static Process Launch(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(typeof(RecoveryCrashProbe).Assembly.Location);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }
    private static string Fixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "wo12-vault-fixture-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "fixture.marker"), "WO12_SYNTHETIC_ONLY");
        using var vault = new LabVaultService(Path.Combine(root, "vault"), crashSafeTestFormat: true);
        var created = vault.Create(Password, LabKdfProfile.LabFast); Require(created.Success, "create");
        // Keep the synthetic code private. Never emit it in evidence or process arguments.
        File.WriteAllText(Path.Combine(root, "private-code.txt"), created.RecoveryCodes[0]);
        Require(vault.Unlock(Password).Success, "unlock");
        var data = new byte[8192]; new Random(12).NextBytes(data);
        Require(vault.ImportBytes("seed.bin", "seed.bin", data).Success, "seed");
        return root;
    }
    internal static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<object>();
        foreach (var stage in new[] { "RecoveryTemporaryCreated", "RecoveryDataWritten", "RecoveryBeforeReplace", "RecoveryAfterReplace" })
        {
            var root = Fixture();
            using (var child = Launch("--recovery-child", root, stage))
            {
                var timer = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(root, "barrier.signal")) && !child.HasExited && timer.Elapsed.TotalSeconds < 30)
                    Thread.Sleep(50);
                Require(File.Exists(Path.Combine(root, "barrier.signal")), "barrier:" + stage);
                child.Kill(true); child.WaitForExit();
            }
            using var recovery = Launch("--recovery-child", root, "none");
            var recoveryText = recovery.StandardOutput.ReadToEnd(); recovery.WaitForExit();
            var consumed = stage == "RecoveryAfterReplace";
            Require(recovery.ExitCode == (consumed ? 4 : 0), "consume-state:" + stage);
            using var reader = Launch("--verify", root);
            var text = reader.StandardOutput.ReadToEnd(); var error = reader.StandardError.ReadToEnd(); reader.WaitForExit();
            Require(reader.ExitCode == 0, "password-reader:" + error);
            using var exported = File.OpenRead(Path.Combine(root,"export","seed.bin"));
            results.Add(new { stage, status = "PASS", consumedBeforeReader = consumed,
                recoveryReaderExit = recovery.ExitCode, recoveryOutput = recoveryText.Trim(),
                passwordReaderExit = reader.ExitCode, passwordOutput = text.Trim(), sha256 = Convert.ToHexString(SHA256.HashData(exported)) });
        }
        var concurrentRoot = Fixture();
        using var first = Launch("--recovery-child", concurrentRoot, "none");
        using var second = Launch("--recovery-child", concurrentRoot, "none");
        var firstOutput = first.StandardOutput.ReadToEnd(); var secondOutput = second.StandardOutput.ReadToEnd();
        first.WaitForExit(); second.WaitForExit();
        Require(new[] { first.ExitCode, second.ExitCode }.Order().SequenceEqual(new[] { 0,4 }), "duplicate-consume");
        File.WriteAllText(Path.Combine(output,"recovery-code-crash.json"), JsonSerializer.Serialize(new {
            status="PASS", scenarios=results, concurrent = new {status="PASS",successes=1,rejections=1,
                firstExit=first.ExitCode,secondExit=second.ExitCode,firstOutput=firstOutput.Trim(),secondOutput=secondOutput.Trim()},
            powerLoss="NOT TESTED", authenticatedStoreReplay="NOT PREVENTED" }, new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine("RECOVERY_CRASH_SCENARIOS=4;FRESH_PASSWORD_READERS=4;BYTES=PASS;CONCURRENT_CONSUME=1_SUCCESS_1_REJECT;POWER_LOSS=NOT_TESTED");
        return 0;
    }
}
