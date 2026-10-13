using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

internal static class TrustedRecoveryProbe
{
    private const string Password="WO15-Synthetic-Only!";
    private const string NewPassword="WO15-New-Synthetic-Only!";
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    internal static string Hash(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    internal static Dictionary<string,string> Hashes(string root)=>Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(f=>Path.GetRelativePath(root,f),Hash);
    private static void Copy(string source,string target){Directory.CreateDirectory(target);foreach(var d in Directory.GetDirectories(source,"*",SearchOption.AllDirectories))Directory.CreateDirectory(Path.Combine(target,Path.GetRelativePath(source,d)));foreach(var f in Directory.GetFiles(source,"*",SearchOption.AllDirectories))File.Copy(f,Path.Combine(target,Path.GetRelativePath(source,f)),true);}
    internal static LabLocalTrustedState Store(string root)=>new(Path.Combine(root,"anchor"),"wo15-test-instance");
    internal static string Seed(string parent,string name,bool legacy=false)
    {
        var root=Path.Combine(parent,"wo15-vault-fixture-"+name);Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"fixture.marker"),"WO15_SYNTHETIC_ONLY");
        using var vault=new LabVaultService(Path.Combine(root,"vault"),crashSafeTestFormat:!legacy);
        var created=vault.Create(Password,LabKdfProfile.LabFast);Check(created.Success,"create");Check(vault.Unlock(Password).Success,"unlock");
        Check(vault.ImportBytes("seed.bin","seed.bin",[1,2,3,4,5]).Success,"import");
        if(!legacy)Store(root).Initialize(vault.CaptureFreshnessEvidence());
        File.WriteAllText(Path.Combine(root,"private-code.txt"),created.RecoveryCodes[0]);return root;
    }
    internal static Process Launch(params string[] args)
    {var p=new ProcessStartInfo("dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};p.ArgumentList.Add(typeof(TrustedRecoveryProbe).Assembly.Location);foreach(var a in args)p.ArgumentList.Add(a);return Process.Start(p)!;}
    internal static void Kill(string root,string mode,string stage)
    {using var p=Launch("--wo15-child",root,mode,stage);var timer=Stopwatch.StartNew();while(!File.Exists(Path.Combine(root,"barrier.signal"))&&!p.HasExited&&timer.Elapsed.TotalSeconds<120)Thread.Sleep(20);Check(File.Exists(Path.Combine(root,"barrier.signal")),"barrier:"+stage+":"+(p.HasExited?p.StandardError.ReadToEnd():"timeout"));p.Kill(true);p.WaitForExit();}
    private static bool ReadBytes(string root,bool newer=false)
    {using var vault=new LabVaultService(Path.Combine(root,"vault"));if(!vault.UnlockReadOnly(newer?NewPassword:Password).Success)return false;var target=Path.Combine(root,"export-"+Guid.NewGuid().ToString("N"));Check(vault.ExportEntry(vault.List().Single().EntryId,target,true).Success,"export");Check(File.ReadAllBytes(Path.Combine(target,"seed.bin")).AsSpan().SequenceEqual(new byte[]{1,2,3,4,5}),"bytes");return true;}
    private static int Child(string root,string mode,string stage)
    {
        LabLocalTrustedState.ValidateFixture(root);
        var counts=new Dictionary<string,int>();
        void Fault(string point){counts[point]=counts.GetValueOrDefault(point)+1; if(point==stage || point+":"+counts[point]==stage){File.WriteAllText(Path.Combine(root,"barrier.signal"),point);Thread.Sleep(Timeout.Infinite);}}
        using var vault=new LabVaultService(Path.Combine(root,"vault"));vault.FaultObserver=Fault;
        if(mode=="legacy"){Check(vault.Unlock(Password).Success,"legacy-open");vault.ReproduceLegacyPasswordChange(Password,NewPassword);return 3;}
        var store=Store(root);store.FaultObserver=Fault;var id=Guid.Parse(File.ReadAllText(Path.Combine(root,"operation.txt")));
        vault.UnlockWithTrustedRecovery(File.ReadAllText(Path.Combine(root,"private-code.txt")),id,store);
        vault.AcknowledgeTrustedRecovery(id,store);return 3;
    }
    internal static int Run(string[] args)
    {
        if(args[0]=="--wo15-memory-child")return MemoryChild(args[1]);
        if(args[0]=="--wo15-memory")return Memory(args[1]);
        if(args[0]=="--wo15-child")return Child(args[1],args[2],args[3]);
        if(args[0]=="--wo15-read"){Console.WriteLine(ReadBytes(args[1],args[2]=="new")?"BYTES_VERIFIED":"PRESERVED_BUT_LOCKED");return 0;}
        Check(args.Length==2&&args[0]=="--wo15","arguments");var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
        var parent=Path.Combine(Path.GetTempPath(),"wo15-private-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(parent);
        var baseline=Seed(parent,"baseline");var replayRoot=Path.Combine(parent,"wo15-vault-fixture-replay");Copy(baseline,replayRoot);
        var slot=Path.Combine(replayRoot,"vault","recovery","recovery_slots.v6.json");var original=File.ReadAllBytes(slot);var oldAnchor=File.ReadAllBytes(Path.Combine(replayRoot,"anchor","state.dpapi"));
        var code=File.ReadAllText(Path.Combine(replayRoot,"private-code.txt"));var id=Guid.NewGuid();
        using(var vault=new LabVaultService(Path.Combine(replayRoot,"vault")))vault.UnlockWithTrustedRecovery(code,id,Store(replayRoot));
        File.WriteAllBytes(slot,original);bool blocked=false;
        using(var vault=new LabVaultService(Path.Combine(replayRoot,"vault"))){try{vault.UnlockWithTrustedRecovery(code,Guid.NewGuid(),Store(replayRoot));}catch(InvalidDataException){blocked=true;}}
        Check(blocked,"slot-replay-blocked");Check(ReadBytes(replayRoot),"readonly-preserved");
        Copy(Path.Combine(baseline,"vault"),Path.Combine(replayRoot,"vault"));
        using(var vault=new LabVaultService(Path.Combine(replayRoot,"vault"))){Check(vault.UnlockReadOnly(Password).Success,"whole-open");Check(Store(replayRoot).Verify(vault.CaptureFreshnessEvidence()).Posture==RecoveryPosture.AuthenticButStale,"whole-stale");}
        File.WriteAllBytes(Path.Combine(replayRoot,"anchor","state.dpapi"),oldAnchor);
        using(var vault=new LabVaultService(Path.Combine(replayRoot,"vault")))Check(vault.UnlockWithTrustedRecovery(code,Guid.NewGuid(),Store(replayRoot)).State==RecoveryReceiptState.ReceiptPending,"joint-rollback-limit");
        File.WriteAllText(Path.Combine(output,"REPLAY_ATTACK_RESULT.json"),JsonSerializer.Serialize(new{status="PARTIAL",vaultOnlySlotReplay="BLOCKED",vaultOnlyWholeReplay="BLOCKED",anchorAndVaultReplay="ACCEPTED_UNSUPPORTED_THREAT_MODEL",readOnlyBytes="PASS",productionApproved=false},Json));
        var receipts=new List<object>();
        foreach(var stage in new[]{"TrustedBeforeAuthentication","TrustedAfterAuthentication","TrustedSessionEstablished","TrustedBeforeConsumption","TrustedAfterConsumption","TrustedBeforeResponse","TrustedResponseReady","TrustedBeforeAcknowledgement","TrustedAfterAcknowledgement","AnchorBeforeReplace","AnchorAfterReplace","AnchorBeforeReplace:2","AnchorAfterReplace:2","AnchorBeforeReplace:3","AnchorAfterReplace:3"})
        {
            var root=Path.Combine(parent,"wo15-vault-fixture-crash-"+stage.Replace(":","-"));Copy(baseline,root);var operation=Guid.NewGuid();File.WriteAllText(Path.Combine(root,"operation.txt"),operation.ToString());Kill(root,"trusted",stage);
            using var reader=Launch("--wo15-read",root,"old");var stdout=reader.StandardOutput.ReadToEnd();var stderr=reader.StandardError.ReadToEnd();reader.WaitForExit();Check(reader.ExitCode==0&&stdout.Contains("BYTES_VERIFIED"),"reader:"+stage+":"+stderr);
            var receipt=Store(root).Query(operation);using var opened=new LabVaultService(Path.Combine(root,"vault"));Check(opened.UnlockReadOnly(Password).Success,"fresh-check-open");var posture=Store(root).Verify(opened.CaptureFreshnessEvidence()).Posture;
            if(stage is "TrustedBeforeResponse" or "TrustedResponseReady" or "TrustedBeforeAcknowledgement")Check(receipt?.State==RecoveryReceiptState.ReceiptPending,"pending-query");
            if(stage=="TrustedAfterAcknowledgement")Check(receipt?.State==RecoveryReceiptState.ReceiptAcknowledged,"ack-query");
            if(stage=="AnchorAfterReplace:2" || stage=="AnchorBeforeReplace:3")Check(receipt?.State==RecoveryReceiptState.ReceiptPending,"committed-query");
            if(stage=="AnchorAfterReplace:3")Check(receipt?.State==RecoveryReceiptState.ReceiptAcknowledged,"acknowledged-query");
            if(stage=="TrustedAfterConsumption" || stage=="AnchorBeforeReplace:2")Check(receipt?.State==RecoveryReceiptState.Interrupted&&posture==RecoveryPosture.AuthenticButStale,"review-required");
            receipts.Add(new{stage,status="PASS",termination="separate-process-kill",readerExit=reader.ExitCode,readerOutput=stdout.Trim(),receipt=receipt?.State.ToString()??"NoReservation",posture=posture.ToString(),powerLoss="NOT TESTED"});
        }
        File.WriteAllText(Path.Combine(output,"RECOVERY_RECEIPT_RESULT.json"),JsonSerializer.Serialize(receipts,Json));
        var legacy=Seed(parent,"legacy-baseline",true);var passwords=new List<object>();
        foreach(var stage in new[]{"LegacyPasswordBeforeHeader","LegacyPrimaryHeaderWritten","LegacySecondaryHeaderWritten","LegacyPasswordHeadersWritten","LegacyPasswordSlotsWritten","LegacyPasswordCommitted"})
        {
            var root=Path.Combine(parent,"wo15-vault-fixture-legacy-"+stage);Copy(legacy,root);var originalHashes=Hashes(Path.Combine(root,"vault"));Kill(root,"legacy",stage);var old=ReadInChild(root,false);var newer=ReadInChild(root,true);
            passwords.Add(new{stage,status=old||newer?"AuthenticatedBytesRecovered":"PreservedButLocked",previousPasswordBytes=old,newPasswordBytes=newer,ciphertextPreserved=originalHashes.Where(p=>p.Key.StartsWith("packs")&&p.Key.EndsWith(".avpack")).All(p=>Hash(Path.Combine(root,"vault",p.Key))==p.Value),before=originalHashes,after=Hashes(Path.Combine(root,"vault")),publicLegacyMutation="BLOCKED"});
        }
        Check(passwords.Count==6,"legacy-count");File.WriteAllText(Path.Combine(output,"V5_PASSWORD_CRASH_RESULT.json"),JsonSerializer.Serialize(passwords,Json));
        File.WriteAllText(Path.Combine(output,"private-fixture-location.txt"),parent);
        Console.WriteLine($"SLOT_REPLAY=BLOCKED;WHOLE_VAULT_REPLAY=BLOCKED;JOINT_ANCHOR_REPLAY=UNSUPPORTED;RECEIPT_CRASH={receipts.Count};LEGACY_PASSWORD_CRASH={passwords.Count};ORIGINAL_BYTES=VERIFIED;POWER_LOSS=NOT_TESTED");return 0;
    }

    private static int Memory(string output)
    {
        Directory.CreateDirectory(output);var results=new List<JsonElement>();
        foreach(var size in new[]{0,4096,4*1024*1024,64*1024*1024,256*1024*1024})
        {
            var root=Path.Combine(Path.GetTempPath(),"wo15-vault-fixture-memory-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"fixture.marker"),"WO15_SYNTHETIC_ONLY");
            var input=Path.Combine(root,"sample.bin");using(var file=File.Create(input))
            {var block=new byte[65536];for(int i=0;i<block.Length;i++)block[i]=(byte)(i%251);for(int left=size;left>0;left-=Math.Min(left,block.Length))file.Write(block,0,Math.Min(left,block.Length));}
            using(var vault=new LabVaultService(Path.Combine(root,"vault"),crashSafeTestFormat:true))
            {Check(vault.Create(Password,LabKdfProfile.LabFast).Success,"memory-create");Check(vault.Unlock(Password).Success,"memory-unlock");Check(vault.ImportFile(input).Success,"memory-import");}
            using var child=Launch("--wo15-memory-child",root);var text=child.StandardOutput.ReadToEnd();var error=child.StandardError.ReadToEnd();child.WaitForExit();Check(child.ExitCode==0,"memory-child:"+error);
            results.Add(JsonDocument.Parse(text).RootElement.Clone());
        }
        File.WriteAllText(Path.Combine(output,"PERFORMANCE_RESULT.json"),JsonSerializer.Serialize(results,Json));
        Console.WriteLine("EXPORT_ONLY_CHILDREN=5;SHA256=PASS;MAX_BYTES=268435456;LOW_END_HARDWARE=NOT_TESTED");return 0;
    }
    private static int MemoryChild(string root)
    {
        LabLocalTrustedState.ValidateFixture(root);using var vault=new LabVaultService(Path.Combine(root,"vault"));
        var unlock=Measure(()=>Check(vault.UnlockReadOnly(Password).Success,"memory-unlock"));
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        var export=Measure(()=>Check(vault.ExportEntry(vault.List().Single().EntryId,Path.Combine(root,"export"),true).Success,"memory-export"));
        var input=Path.Combine(root,"sample.bin");var output=Path.Combine(root,"export","sample.bin");string? before=null,after=null;
        var hashing=Measure(()=>{before=Hash(input);after=Hash(output);});Check(before==after,"memory-hash");
        Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",bytes=new FileInfo(input).Length,inputSha256=before,outputSha256=after,unlock,export,hashing,importInThisProcess=false,exportIncludesAuthenticationAndOutput=true,workingSetIncludesPriorUnlock=true,managedAllocationIncludesSampler=true,lowEndHardware="NOT TESTED"}));return 0;
    }
    private static object Measure(Action action)
    {
        using var process=Process.GetCurrentProcess();process.Refresh();long initial=process.WorkingSet64,max=initial;
        var allocated=GC.GetTotalAllocatedBytes(true);var cpu=process.TotalProcessorTime;var timer=Stopwatch.StartNew();
        using var stop=new CancellationTokenSource();var sampler=Task.Run(()=>{while(!stop.IsCancellationRequested){process.Refresh();max=Math.Max(max,process.WorkingSet64);Thread.Sleep(5);}});
        try{action();}finally{stop.Cancel();sampler.Wait();}process.Refresh();max=Math.Max(max,process.WorkingSet64);
        return new{elapsedMs=timer.Elapsed.TotalMilliseconds,cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,managedAllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated,initialWorkingSetBytes=initial,maxSampledWorkingSetBytes=max};
    }
    private static bool ReadInChild(string root,bool newer)
    {using var child=Launch("--wo15-read",root,newer?"new":"old");var text=child.StandardOutput.ReadToEnd();var error=child.StandardError.ReadToEnd();child.WaitForExit();Check(child.ExitCode==0,"password-reader:"+error);return text.Contains("BYTES_VERIFIED");}
}
