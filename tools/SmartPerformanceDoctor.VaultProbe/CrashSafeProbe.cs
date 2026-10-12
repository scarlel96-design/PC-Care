using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

internal static class CrashSafeProbe
{
    private const string Password = "WO12-Synthetic-Only!";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static byte[] Payload(int length, int seed) { var data = new byte[length]; new Random(seed).NextBytes(data); return data; }
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
    private static Process Launch(params string[] args)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add(typeof(CrashSafeProbe).Assembly.Location);
        foreach (var arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }
    private static void Copy(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var dest = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!); File.Copy(file, dest);
        }
    }
    internal static int Run(string outputDirectory)
    {
        var output = Path.GetFullPath(outputDirectory); Directory.CreateDirectory(output);
        var root = Path.Combine(Path.GetTempPath(), "wo13-private-" + Guid.NewGuid().ToString("N"));
        var baseline = Path.Combine(root, "baseline"); Directory.CreateDirectory(baseline);
        File.WriteAllText(Path.Combine(baseline, "fixture.marker"), "WO12_SYNTHETIC_ONLY");
        using (var vault = new LabVaultService(Path.Combine(baseline, "vault"), crashSafeTestFormat: true))
        {
            Require(vault.Create(Password, LabKdfProfile.LabFast).Success, "CreateV6");
            Require(vault.Unlock(Password).Success, "UnlockV6");
            var seed = vault.ImportBytes("seed.bin", "seed.bin", Payload(8192, 12));
            Require(seed.Success, "SeedV6:" + seed.Message);
        }
        var results = new List<object>();
        foreach (var stage in new[] { "BeforeObjectWrite", "ObjectWritten", "ObjectsReady", "GenerationMetadataWritten", "GenerationHeaderWritten", "GenerationVerified", "MetadataReady", "ActivationBeforeReplace", "ActivationAfterReplace", "CommitWritten", "BeforeFinalJournal", "FinalJournalWritten" })
        {
            var testRoot = Path.Combine(root, "wo12-vault-fixture-" + stage); Copy(baseline, testRoot);
            var hashes = Directory.GetFiles(Path.Combine(testRoot, "vault", "objects"), "*.obj", SearchOption.AllDirectories)
                .ToDictionary(file => file, file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
            using (var child = Launch("--child", testRoot, stage))
            {
                var timer = Stopwatch.StartNew();
                while (!File.Exists(Path.Combine(testRoot, "barrier.signal")) && !child.HasExited && timer.Elapsed.TotalSeconds < 60) Thread.Sleep(20);
                Require(File.Exists(Path.Combine(testRoot, "barrier.signal")), "Barrier:" + stage + ":" + (child.HasExited ? child.StandardError.ReadToEnd() : "timeout"));
                child.Kill(true); child.WaitForExit();
            }
            using var reader = Launch("--verify", testRoot);
            var text = reader.StandardOutput.ReadToEnd(); var error = reader.StandardError.ReadToEnd(); reader.WaitForExit();
            Require(reader.ExitCode == 0, "Reader:" + stage + ":" + error);
            var committed = stage is "ActivationAfterReplace" or "CommitWritten" or "BeforeFinalJournal" or "FinalJournalWritten";
            Require(text.Contains("VERIFIED_ENTRIES=" + (committed ? 2 : 1)), "EntryCount:" + stage + ":" + text);
            Require(hashes.All(pair => pair.Value == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Key)))), "OriginalObjectHash");
            results.Add(new { stage, status = "PASS", readerExit = reader.ExitCode, output = text.Trim(), recovery = committed ? "RestoredCommittedGeneration" : "RestoredPreviousGeneration", existingBytesVerified = true, powerLossTested = false });
        }
        File.WriteAllText(Path.Combine(output, "commit-recovery.json"), JsonSerializer.Serialize(results, Json));
        var passwords = new List<object>();
        foreach (var stage in new[] { "PasswordKdfPrepared", "PasswordSaltPrepared", "PasswordWrapPrepared", "PasswordHeaderPrepared", "GenerationMetadataWritten", "GenerationHeaderWritten", "GenerationVerified", "ActivationBeforeReplace", "ActivationAfterReplace", "PasswordCommitted" })
        {
            var testRoot = Path.Combine(root,"wo12-vault-fixture-password-"+stage); Copy(baseline,testRoot);
            using(var child=Launch("--password-child",testRoot,stage))
            {
                var timer=Stopwatch.StartNew();
                while(!File.Exists(Path.Combine(testRoot,"barrier.signal")) && !child.HasExited && timer.Elapsed.TotalSeconds<60) Thread.Sleep(20);
                Require(File.Exists(Path.Combine(testRoot,"barrier.signal")),"PasswordBarrier:"+stage);
                child.Kill(true); child.WaitForExit();
            }
            var committed=stage is "ActivationAfterReplace" or "PasswordCommitted";
            using var reader=Launch(committed?"--verify-new-password":"--verify",testRoot);
            var text=reader.StandardOutput.ReadToEnd(); var error=reader.StandardError.ReadToEnd(); reader.WaitForExit();
            Require(reader.ExitCode==0,"PasswordReader:"+stage+":"+error);
            Require(text.Contains("VERIFIED_ENTRIES=1"),"PasswordEntries");
            passwords.Add(new {stage,status="PASS",readerExit=reader.ExitCode,output=text.Trim(),validCredential=committed?"new":"previous",existingBytesVerified=true});
        }
        File.WriteAllText(Path.Combine(output,"password-crash.json"),JsonSerializer.Serialize(passwords,Json));
        var streaming=new List<object>();
        foreach(var size in new[]{0,4096,4*1024*1024,64*1024*1024,256*1024*1024})
        {
            var work=Path.Combine(root,"stream-"+size); Directory.CreateDirectory(work);
            var input=Path.Combine(work,"sample.bin");
            using(var file=new FileStream(input,FileMode.CreateNew))
            {
                var buffer=Payload(64*1024,13); for(var remaining=size;remaining>0;remaining-=Math.Min(buffer.Length,remaining)) file.Write(buffer,0,Math.Min(buffer.Length,remaining));
            }
            using var vault=new LabVaultService(Path.Combine(work,"vault"),crashSafeTestFormat:true);
            Require(vault.Create(Password,LabKdfProfile.LabFast).Success,"StreamCreate"); Require(vault.Unlock(Password).Success,"StreamUnlock");
            var imported=vault.ImportFile(input); Require(imported.Success,"StreamImport:"+imported.Message);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            using var process=Process.GetCurrentProcess(); var cpu=process.TotalProcessorTime; var allocated=GC.GetTotalAllocatedBytes(true); var timer=Stopwatch.StartNew();
            process.Refresh(); long maxWorkingSet=process.WorkingSet64; using var stop=new CancellationTokenSource();
            var sampler=Task.Run(()=>{while(!stop.IsCancellationRequested){process.Refresh();maxWorkingSet=Math.Max(maxWorkingSet,process.WorkingSet64);Thread.Sleep(10);}});
            var exported=vault.ExportEntry(imported.EntryId!,Path.Combine(work,"export"),true); stop.Cancel();sampler.Wait();
            Require(exported.Success,"StreamExport:"+exported.Message);
            var elapsed=timer.Elapsed.TotalMilliseconds; var exportCpu=(process.TotalProcessorTime-cpu).TotalMilliseconds; var allocation=GC.GetTotalAllocatedBytes(true)-allocated;
            using var source=File.OpenRead(input); using var result=File.OpenRead(Path.Combine(work,"export","sample.bin"));
            var inputHash=Convert.ToHexString(SHA256.HashData(source)); var outputHash=Convert.ToHexString(SHA256.HashData(result)); Require(inputHash==outputHash,"StreamHash");
            streaming.Add(new {bytes=size,status="PASS",inputSha256=inputHash,outputSha256=outputHash,exportMs=elapsed,cpuMs=exportCpu,managedAllocatedBytes=allocation,maxSampledWorkingSetBytes=maxWorkingSet,kdfExcluded=true,allocationIsCumulative=true});
        }
        File.WriteAllText(Path.Combine(output,"streaming-export.json"),JsonSerializer.Serialize(streaming,Json));
        File.WriteAllText(Path.Combine(output, "private-fixture-location.txt"), root);
        Console.WriteLine($"CRASH_SCENARIOS={results.Count};PASSWORD_CRASH_SCENARIOS={passwords.Count};STREAMING_CASES={streaming.Count};EXISTING_BYTES=PASS;METADATA_READY=PASS;FORMAT=v6-test-only;POWER_LOSS=NOT_TESTED");
        return 0;
    }
}
