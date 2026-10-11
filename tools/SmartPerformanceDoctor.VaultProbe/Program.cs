using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

// This tool accepts only its own generated fixture root.
const string Password = "WO12-Synthetic-Only!";
var json = new JsonSerializerOptions { WriteIndented = true };
static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
static byte[] Payload(int length, int seed) { var bytes=new byte[length]; new Random(seed).NextBytes(bytes); return bytes; }
static void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }
static string ValidateRoot(string root)
{
    root=Path.GetFullPath(root);
    Check(Path.GetFileName(root).StartsWith("wo12-vault-fixture-",StringComparison.Ordinal),"FixtureRootRequired");
    Check(File.ReadAllText(Path.Combine(root,"fixture.marker"))=="WO12_SYNTHETIC_ONLY","FixtureMarkerRequired");
    return root;
}
if(args.Length==3 && args[0]=="--child")
{
    var root=ValidateRoot(args[1]);
    using var vault=new LabVaultService(Path.Combine(root,"vault"));
    Check(vault.Unlock(Password).Success,"ChildUnlock");
    vault.FaultObserver=stage=>
    {
        if(stage!=args[2]) return;
        File.WriteAllText(Path.Combine(root,"barrier.signal"),stage);
        Thread.Sleep(Timeout.Infinite);
    };
    Check(vault.ImportBytes("new.bin","new.bin",Payload(4096,13)).Success,"ChildImport");
    return 3;
}
if(args.Length==2 && args[0]=="--verify")
{
    var root=ValidateRoot(args[1]);
    using var vault=new LabVaultService(Path.Combine(root,"vault"));
    var opened=vault.Unlock(Password);
    if(!opened.Success) { Console.WriteLine("PRESERVED_FAIL_CLOSED"); return 4; }
    vault.FaultObserver=message=>Console.Error.WriteLine(message);
    var entries=vault.List();
    foreach(var entry in entries)
    {
        var output=Path.Combine(root,"export");Directory.CreateDirectory(output);
        var exported=vault.ExportEntry(entry.EntryId,output,true); Check(exported.Success,"ReaderExport:"+exported.Message);
        var expected=Payload(entry.DisplayName=="seed.bin"?8192:4096,entry.DisplayName=="seed.bin"?12:13);
        Check(File.ReadAllBytes(Path.Combine(output,entry.DisplayName)).AsSpan().SequenceEqual(expected),"ReaderBytes");
    }
    Console.WriteLine($"VERIFIED_ENTRIES={entries.Count};WRITE_ALLOWED={vault.IsWriteAllowed}");
    return 0;
}
if(args.Length!=2 || args[0]!="--synthetic") return 2;
var outputRoot=Path.GetFullPath(args[1]);Directory.CreateDirectory(outputRoot);
var fixtureRoot=Path.Combine(Path.GetTempPath(),"wo12-vault-fixture-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);File.WriteAllText(Path.Combine(fixtureRoot,"fixture.marker"),"WO12_SYNTHETIC_ONLY");
Process Launch(params string[] values)
{
    var start=new ProcessStartInfo("dotnet") { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true };
    start.ArgumentList.Add(typeof(Program).Assembly.Location);
    foreach(var value in values)start.ArgumentList.Add(value);
    return Process.Start(start)!;
}
var results=new List<object>();
foreach(var stage in new[]{"ObjectsReady","MetadataReady","CommitWritten"})
{
    var root=Path.Combine(fixtureRoot,"wo12-vault-fixture-"+stage);Directory.CreateDirectory(root);
    File.WriteAllText(Path.Combine(root,"fixture.marker"),"WO12_SYNTHETIC_ONLY");
    using(var vault=new LabVaultService(Path.Combine(root,"vault")))
    {
        Check(vault.Create(Password,LabKdfProfile.LabFast).Success,"Create");Check(vault.Unlock(Password).Success,"Unlock");
        Check(vault.ImportBytes("seed.bin","seed.bin",Payload(8192,12)).Success,"Seed");
    }
    var objectHashes=Directory.GetFiles(Path.Combine(root,"vault"),"*",SearchOption.AllDirectories)
        .Where(p=>p.EndsWith(".obj") || p.EndsWith(".avpack",StringComparison.OrdinalIgnoreCase))
        .ToDictionary(p=>p, p=>File.ReadAllBytes(p));
    using(var child=Launch("--child",root,stage))
    {
        var wait=Stopwatch.StartNew();
        while(!File.Exists(Path.Combine(root,"barrier.signal")) && !child.HasExited && wait.Elapsed.TotalSeconds<30)Thread.Sleep(50);
        Check(File.Exists(Path.Combine(root,"barrier.signal")),"BarrierReached");child.Kill(true);child.WaitForExit();
    }
    // A new reader process performs unlock, export, and byte comparison.
    using var reader=Launch("--verify",root);var text=reader.StandardOutput.ReadToEnd();var error=reader.StandardError.ReadToEnd();reader.WaitForExit();
    var expectedExit=stage=="MetadataReady"?4:0;
    Check(reader.ExitCode==expectedExit,"ReaderExit:"+error);
    // Packed storage can grow. The original prefix must remain unchanged.
    var originalPreserved=objectHashes.All(pair=>File.Exists(pair.Key) && File.ReadAllBytes(pair.Key).AsSpan().StartsWith(pair.Value));
    Check(originalPreserved,"OriginalObjectsMissing");
    results.Add(new { stage,status=stage=="MetadataReady"?"PARTIAL":"PASS",readerExit=reader.ExitCode,output=text.Trim(),
        originalObjectFilesPresent=originalPreserved,recovery=stage=="MetadataReady"?"Unlock blocked; ciphertext and metadata retained for review":"Export bytes verified in separate process",
        powerLossTested=false });
}
var clock=Stopwatch.StartNew();using var process=Process.GetCurrentProcess();var cpuBefore=process.TotalProcessorTime;
var vaultRoot=Path.Combine(fixtureRoot,"performance-vault");
string inputHash,outputHash;double unlockMs,importMs,exportMs;
using(var vault=new LabVaultService(vaultRoot))
{
    Check(vault.Create(Password,LabKdfProfile.LabFast).Success,"PerfCreate");var timer=Stopwatch.StartNew();Check(vault.Unlock(Password).Success,"PerfUnlock");unlockMs=timer.Elapsed.TotalMilliseconds;
    var data=Payload(4*1024*1024,99);inputHash=Hash(data);timer.Restart();var imported=vault.ImportBytes("sample.bin","sample.bin",data);Check(imported.Success,"PerfImport");importMs=timer.Elapsed.TotalMilliseconds;
    Directory.CreateDirectory(Path.Combine(fixtureRoot,"perf-export"));timer.Restart();Check(vault.ExportEntry(imported.EntryId!,Path.Combine(fixtureRoot,"perf-export"),true).Success,"PerfExport");exportMs=timer.Elapsed.TotalMilliseconds;
    outputHash=Hash(File.ReadAllBytes(Path.Combine(fixtureRoot,"perf-export","sample.bin")));Check(inputHash==outputHash,"PerfHash");
}
var performance=new { status="PASS",bytes=4*1024*1024,inputSha256=inputHash,outputSha256=outputHash,unlockMs,importMs,exportMs,
    kdf="LabFast synthetic profile",elapsedMs=clock.Elapsed.TotalMilliseconds,cpuMs=(process.TotalProcessorTime-cpuBefore).TotalMilliseconds,
    peakWorkingSetBytes=process.PeakWorkingSet64,lowEndHardware="NOT TESTED",largeList="NOT TESTED",fixtureRoot };
File.WriteAllText(Path.Combine(outputRoot,"crash-results.json"),JsonSerializer.Serialize(results,json));
// Keep private fixture paths out of published metrics.
File.WriteAllText(Path.Combine(outputRoot,"performance.json"),JsonSerializer.Serialize(new {performance.status,performance.bytes,performance.inputSha256,performance.outputSha256,performance.unlockMs,performance.importMs,performance.exportMs,performance.kdf,performance.elapsedMs,performance.cpuMs,performance.peakWorkingSetBytes,performance.lowEndHardware,performance.largeList},json));
File.WriteAllText(Path.Combine(outputRoot,"private-fixture-location.txt"),fixtureRoot);
Console.WriteLine("CRASH_SCENARIOS=3;SEPARATE_READER_PROCESSES=3;HASH_ROUNDTRIP=PASS;FAIL_CLOSED=1;POWER_LOSS=NOT_TESTED");
return 0;
