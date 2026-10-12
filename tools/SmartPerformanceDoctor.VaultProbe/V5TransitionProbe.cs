using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

internal static class V5TransitionProbe
{
    private const string Password = "WO14-Synthetic-Only!";
    private const string NewPassword = "WO14-New-Synthetic-Only!";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static void Require(bool value, string name) { if (!value) throw new InvalidOperationException(name); }
    private static byte[] Payload(int size, int seed) { var b=new byte[size]; new Random(seed).NextBytes(b); return b; }
    private static string Hash(string path) { using var input=File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(input)); }
    private static Dictionary<string,string> Hashes(string root) => Directory.GetFiles(root,"*",SearchOption.AllDirectories)
        .ToDictionary(p=>Path.GetRelativePath(root,p),Hash);
    private static bool Same(Dictionary<string,string> a, Dictionary<string,string> b) => a.Count==b.Count && a.All(p=>b.TryGetValue(p.Key,out var h)&&h==p.Value);
    private static void Copy(string source,string target)
    {
        Directory.CreateDirectory(target);
        foreach(var directory in Directory.GetDirectories(source,"*",SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(target,Path.GetRelativePath(source,directory)));
        foreach(var file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)) File.Copy(file,Path.Combine(target,Path.GetRelativePath(source,file)));
    }
    private static Process Launch(params string[] args)
    {
        var start=new ProcessStartInfo("dotnet") { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true };
        start.ArgumentList.Add(typeof(V5TransitionProbe).Assembly.Location);
        foreach(var arg in args) start.ArgumentList.Add(arg);
        return Process.Start(start)!;
    }
    private static (int Exit,string Output) Read(string root,bool newer=false)
    {
        using var child=Launch("--wo14-read",root,newer?"new":"old");
        var output=child.StandardOutput.ReadToEnd(); var error=child.StandardError.ReadToEnd(); child.WaitForExit();
        Require(child.ExitCode is 0 or 4,"ReaderFailure:"+error); return (child.ExitCode,output.Trim());
    }
    private static int Reader(string root,bool newer)
    {
        using var vault=new LabVaultService(root);
        if(!vault.UnlockReadOnly(newer?NewPassword:Password).Success) { Console.WriteLine("PRESERVED_BUT_LOCKED"); return 4; }
        var exports=Path.Combine(Path.GetDirectoryName(root)!,"export-"+Guid.NewGuid().ToString("N"));
        var hashes=new Dictionary<string,string>();
        foreach(var entry in vault.List())
        {
            var parent=Path.Combine(exports,Path.GetDirectoryName(entry.RelativePath.Replace('/',Path.DirectorySeparatorChar))??"");
            Directory.CreateDirectory(parent); Require(vault.ExportEntry(entry.EntryId,parent,true).Success,"ReaderExport");
            var file=Path.Combine(parent,entry.DisplayName);
            var expected=entry.DisplayName=="seed.bin"?Payload(8192,14):entry.DisplayName=="empty.bin"?[]:Payload(4096,15);
            Require(File.ReadAllBytes(file).AsSpan().SequenceEqual(expected),"ReaderBytes");
            hashes.Add(entry.RelativePath,Hash(file));
        }
        Require(hashes.ContainsKey("폴더/하위/seed.bin")&&hashes.ContainsKey("empty.bin"),"ReaderMissingOriginal");
        Console.WriteLine(JsonSerializer.Serialize(new {status="PASS",entries=hashes.Count,hashes})); return 0;
    }
    private static void KillAt(string root,string mode,string stage)
    {
        using var child=Launch("--wo14-child",root,mode,stage);
        var signal=Path.Combine(root,"barrier.signal"); var timer=Stopwatch.StartNew();
        while(!File.Exists(signal)&&!child.HasExited&&timer.Elapsed.TotalSeconds<90) Thread.Sleep(20);
        Require(File.Exists(signal),"Barrier:"+stage+":"+(child.HasExited?child.StandardError.ReadToEnd():"timeout"));
        child.Kill(true); child.WaitForExit();
    }
    private static int Child(string root,string mode,string stage)
    {
        void Fault(string point) { if(point!=stage)return; File.WriteAllText(Path.Combine(root,"barrier.signal"),point); Thread.Sleep(Timeout.Infinite); }
        if(mode is "transition" or "salvage")
            LabV5Transition.ExecuteSynthetic(Path.Combine(root,"source"),Path.Combine(root,"work-child"),Password,true,fault:Fault,salvageReadOnly:mode=="salvage");
        else
        {
            using var vault=new LabVaultService(Path.Combine(root,"source")); Require(vault.Unlock(Password).Success,"ChildUnlock");
            vault.FaultObserver=Fault;
            if(mode=="password") Require(vault.ChangePassword(Password,NewPassword).Success,"ChildPassword");
            else Require(vault.ImportBytes("new.bin","new.bin",Payload(4096,15)).Success,"ChildImport");
        }
        return 3;
    }
    private static string Fixture(string parent,string name,string source)
    {
        var root=Path.Combine(parent,"wo14-vault-fixture-"+name); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root,"fixture.marker"),"WO14_SYNTHETIC_ONLY"); Copy(source,Path.Combine(root,"source")); return root;
    }
    internal static int Run(string[] args)
    {
        if(args[0]=="--wo14-read") return Reader(args[1],args[2]=="new");
        if(args[0]=="--wo14-child") return Child(args[1],args[2],args[3]);
        if(args[0]=="--wo14-memory-child") return MemoryChild(args[1],int.Parse(args[2]));
        if(args[0]=="--wo14-memory") return Memory(args[1]);
        Require(args.Length==2&&args[0]=="--wo14","ProbeArguments");
        var output=Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
        var parent=Path.Combine(Path.GetTempPath(),"wo14-private-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(parent);
        var baseline=Path.Combine(parent,"baseline");
        using(var vault=new LabVaultService(baseline))
        {
            Require(vault.Create(Password,LabKdfProfile.LabFast).Success,"CreateV5"); Require(vault.Unlock(Password).Success,"OpenV5");
            Require(vault.ImportBytes("seed.bin","폴더/하위/seed.bin",Payload(8192,14)).Success,"SeedV5");
            Require(vault.ImportBytes("empty.bin","empty.bin",[]).Success,"EmptyV5");
        }
        var healthy=Fixture(parent,"healthy",baseline); var source=Path.Combine(healthy,"source"); var before=Hashes(source);
        var result=LabV5Transition.ExecuteSynthetic(source,Path.Combine(healthy,"work-complete"),Password,true);
        Require(result.Completed&&Same(before,Hashes(source)),"TransitionOriginalPreservation");
        var converted=Path.Combine(healthy,"work-complete","candidate-v6");
        Copy(source,Path.Combine(parent,"reader-old")); var old=Read(Path.Combine(parent,"reader-old")); var fresh=Read(converted);
        Require(old.Exit==0&&fresh.Exit==0,"BothFormatsRead");
        File.WriteAllText(Path.Combine(output,"transition.json"),JsonSerializer.Serialize(new {status="PASS",sourceBefore=before,sourceAfter=Hashes(source),sourceBytes=result.SourceBytes,contentHashes=result.ContentHashes,oldReader=old.Output,newReader=fresh.Output,format="v6-test-only",rekeyed=false},Json));
        var transitions=new List<object>();
        foreach(var stage in new[]{"TransitionSnapshotReady","TransitionSourceAuthenticated","TransitionContentVerified","TransitionRecoveryReady","GenerationMetadataWritten","GenerationHeaderWritten","GenerationVerified","ActivationBeforeReplace","ActivationAfterReplace","TransitionVerified"})
        {
            var root=Fixture(parent,"transition-"+stage,baseline); var original=Path.Combine(root,"source"); var hashes=Hashes(original);
            KillAt(root,"transition",stage); Require(Same(hashes,Hashes(original)),"CrashOriginalHash");
            var readerCopy=Path.Combine(root,"original-reader"); Copy(original,readerCopy); var reader=Read(readerCopy); Require(reader.Exit==0,"CrashOriginalReader");
            // A new workspace is required. Partial candidates remain as evidence.
            var retry=LabV5Transition.ExecuteSynthetic(original,Path.Combine(root,"work-retry"),Password,true);
            Require(retry.Completed&&Same(hashes,Hashes(original)),"FreshWorkspaceRetry");
            transitions.Add(new {stage,status="PASS",readerExit=reader.Exit,readerOutput=reader.Output,originalUnchanged=true,retry="fresh-workspace-only",partialCandidatePreserved=true});
        }
        File.WriteAllText(Path.Combine(output,"transition-crash.json"),JsonSerializer.Serialize(transitions,Json));
        var metadata=new List<object>();
        foreach(var version in new[]{5,6})
        {
            var root=Fixture(parent,"metadata-v"+version,version==5?baseline:converted);
            KillAt(root,"import","MetadataReady"); var original=Path.Combine(root,"source"); var hashes=Hashes(original);
            var readerCopy=Path.Combine(root,"normal-reader"); Copy(original,readerCopy); var normal=Read(readerCopy);
            if(version==5)
            {
                Require(normal.Exit==4,"LegacyMustRemainBlocked");
                var salvage=LabV5Transition.ExecuteSynthetic(original,Path.Combine(root,"work-salvage"),Password,true,salvageReadOnly:true);
                Require(salvage.Status=="ReadOnlyRecovery"&&salvage.ContentHashes.Count==3,"SalvageAuthenticatedContents");
                Require(File.ReadAllBytes(Path.Combine(root,"work-salvage","recovered","폴더","하위","seed.bin")).AsSpan().SequenceEqual(Payload(8192,14)),"SalvageOriginalBytes");
                Require(Same(hashes,Hashes(original)),"SalvageSourceHash");
                var repeat=Fixture(parent,"salvage-interrupted",original); KillAt(repeat,"salvage","TransitionRecoveryExported");
                var retry=LabV5Transition.ExecuteSynthetic(Path.Combine(repeat,"source"),Path.Combine(repeat,"work-retry"),Password,true,salvageReadOnly:true);
                Require(retry.ContentHashes.Count==3,"SalvageRepeat");
                metadata.Add(new {version,status="ReadOnlyRecovery",normalStatus="PRESERVED_BUT_LOCKED",normalExit=normal.Exit,authenticatedExports=3,originalUnchanged=true,commitHistoryQualified=false,recoveryInterruptedAndRetried=true,sourceBefore=hashes,sourceAfter=Hashes(original)});
            }
            else { Require(normal.Exit==0,"ConvertedMetadataReadyReader"); metadata.Add(new {version,status="RestoredPreviousGeneration",readerExit=normal.Exit,readerOutput=normal.Output}); }
        }
        File.WriteAllText(Path.Combine(output,"metadata-ready.json"),JsonSerializer.Serialize(metadata,Json));
        var passwords=new List<object>();
        foreach(var stage in new[]{"PasswordKdfPrepared","PasswordSaltPrepared","PasswordWrapPrepared","PasswordHeaderPrepared","GenerationMetadataWritten","GenerationHeaderWritten","GenerationVerified","ActivationBeforeReplace","ActivationAfterReplace","PasswordCommitted"})
        {
            var root=Fixture(parent,"password-"+stage,converted); KillAt(root,"password",stage);
            Copy(Path.Combine(root,"source"),Path.Combine(root,"reader-old")); Copy(Path.Combine(root,"source"),Path.Combine(root,"reader-new"));
            var previous=Read(Path.Combine(root,"reader-old")); var newer=Read(Path.Combine(root,"reader-new"),true);
            var committed=stage is "ActivationAfterReplace" or "PasswordCommitted";
            Require((committed?newer.Exit:previous.Exit)==0,"PasswordBytes:"+stage);
            passwords.Add(new {stage,status="PASS",previousExit=previous.Exit,newExit=newer.Exit,previousOutput=previous.Output,newOutput=newer.Output,format="explicitly-converted-v6"});
        }
        File.WriteAllText(Path.Combine(output,"password-crash.json"),JsonSerializer.Serialize(passwords,Json));
        // Valid historical stores are authenticated but lack an external freshness anchor.
        var replay=Fixture(parent,"replay",converted); var replayVault=Path.Combine(replay,"source");
        var storePath=Path.Combine(replayVault,"recovery","recovery_slots.v6.json"); var priorStore=File.ReadAllBytes(storePath);
        using(var vault=new LabVaultService(replayVault)) { Require(vault.UnlockWithRecoveryCode(result.FreshRecoveryCodes[0]).Success,"CodeFirstUse"); vault.Lock(); Require(!vault.UnlockWithRecoveryCode(result.FreshRecoveryCodes[0]).Success,"CodeDuplicateDenied"); }
        File.WriteAllBytes(storePath,priorStore);
        using(var vault=new LabVaultService(replayVault)) Require(vault.UnlockWithRecoveryCode(result.FreshRecoveryCodes[0]).Success,"ReplayLimitationReproduced");
        var whole = Fixture(parent,"whole-container-replay",converted);
        var wholeRoot = Path.Combine(whole,"source");
        using(var vault=new LabVaultService(wholeRoot)) Require(vault.UnlockWithRecoveryCode(result.FreshRecoveryCodes[0]).Success,"WholeReplayFirstUse");
        foreach(var file in Directory.GetFiles(converted,"*",SearchOption.AllDirectories))
            File.Copy(file,Path.Combine(wholeRoot,Path.GetRelativePath(converted,file)),true);
        using(var vault=new LabVaultService(wholeRoot)) Require(vault.UnlockWithRecoveryCode(result.FreshRecoveryCodes[0]).Success,"WholeReplayAccepted");
        var activation = Fixture(parent,"activation-replay",converted);
        var activationRoot = Path.Combine(activation,"source");
        var oldActivation = File.ReadAllBytes(Path.Combine(activationRoot,"generations-v6","active.json"));
        using(var vault=new LabVaultService(activationRoot))
        {
            Require(vault.Unlock(Password).Success,"ActivationOpen");
            Require(vault.ChangePassword(Password,NewPassword).Success,"ActivationPasswordChange");
        }
        File.WriteAllBytes(Path.Combine(activationRoot,"generations-v6","active.json"),oldActivation);
        var activationReader = Read(activationRoot);
        Require(activationReader.Exit==0,"HistoricalActivationReplayAccepted");
        File.WriteAllText(Path.Combine(output,"replay.json"),JsonSerializer.Serialize(new {status="PARTIAL",integrityProtection="HMAC",duplicateWithoutReplay="rejected",validStoreReplay="accepted",wholeContainerReplay="accepted",historicalActivationReplay="accepted",historicalActivationReader=activationReader.Output,externalFreshnessAnchor="absent",productionApproved=false},Json));
        File.WriteAllText(Path.Combine(output,"private-fixture-location.txt"),parent);
        Console.WriteLine($"TRANSITION=PASS;TRANSITION_CRASH={transitions.Count};METADATA_READY_V5=READ_ONLY_RECOVERY;METADATA_READY_V6=PASS;PASSWORD_CRASH={passwords.Count};REPLAY=UNMITIGATED;POWER_LOSS=NOT_TESTED"); return 0;
    }
    private static int Memory(string output)
    {
        Directory.CreateDirectory(output); var results=new List<JsonElement>();
        foreach(var size in new[]{4096,4*1024*1024,64*1024*1024,256*1024*1024})
        {
            using var child=Launch("--wo14-memory-child",Path.GetFullPath(output),size.ToString());
            var text=child.StandardOutput.ReadToEnd(); var error=child.StandardError.ReadToEnd(); child.WaitForExit(); Require(child.ExitCode==0,"MemoryChild:"+error);
            results.Add(JsonDocument.Parse(text).RootElement.Clone());
        }
        File.WriteAllText(Path.Combine(output,"streaming-memory.json"),JsonSerializer.Serialize(results,Json)); Console.WriteLine("FRESH_PROCESSES=4;SHA256=PASS;KDF_AND_EXPORT=SEPARATE_PHASES;LOW_END_HARDWARE=NOT_TESTED");return 0;
    }
    private static int MemoryChild(string output,int size)
    {
        var root=Path.Combine(Path.GetTempPath(),"wo14-memory-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var input=Path.Combine(root,"sample.bin");using(var file=File.Create(input)) { var buffer=Payload(65536,14);for(int left=size;left>0;left-=Math.Min(left,buffer.Length))file.Write(buffer,0,Math.Min(left,buffer.Length)); }
        using var vault=new LabVaultService(Path.Combine(root,"vault"),crashSafeTestFormat:true);
        Require(vault.Create(Password,LabKdfProfile.LabFast).Success,"MemoryCreate");
        var kdf=Measure(()=>Require(vault.Unlock(Password).Success,"MemoryUnlock"));
        var imported=vault.ImportFile(input);Require(imported.Success,"MemoryImport");
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        var export=Measure(()=>Require(vault.ExportEntry(imported.EntryId!,Path.Combine(root,"export"),true).Success,"MemoryExport"));
        var original=Hash(input);var extracted=Hash(Path.Combine(root,"export","sample.bin"));Require(original==extracted,"MemoryHash");
        Console.WriteLine(JsonSerializer.Serialize(new {bytes=size,status="PASS",inputSha256=original,outputSha256=extracted,unlock=kdf,export,allocationIsCumulative=true,workingSetIncludesPriorKdfAndImport=true,processPerSize=true}));return 0;
    }
    private static object Measure(Action action)
    {
        using var process=Process.GetCurrentProcess();process.Refresh();long initial=process.WorkingSet64,max=initial;var allocation=GC.GetTotalAllocatedBytes(true);var cpu=process.TotalProcessorTime;var timer=Stopwatch.StartNew();
        using var stop=new CancellationTokenSource();var sampler=Task.Run(()=>{while(!stop.IsCancellationRequested){process.Refresh();max=Math.Max(max,process.WorkingSet64);Thread.Sleep(5);}});
        try {action();}finally {stop.Cancel();sampler.Wait();}
        return new {elapsedMs=timer.Elapsed.TotalMilliseconds,cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,managedAllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocation,initialWorkingSetBytes=initial,maxSampledWorkingSetBytes=max};
    }
}
