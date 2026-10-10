using SmartPerformanceDoctor.Diagnostics;
using System.Diagnostics;
using System.Text.Json;

internal static class ControlledWorkload
{
    public static async Task<int> Run(string[] args)
    {
        if (args.Length != 5) throw new ArgumentException("--workload MODE SECONDS OUTPUT_DIRECTORY THREADS");
        var policy = new WorkloadPolicy(args[1], int.Parse(args[2]), int.Parse(args[4])); policy.Validate();
        var root = Path.GetFullPath(args[3]); Directory.CreateDirectory(root);
        var resultPath = Path.Combine(root,"workload-"+Guid.NewGuid().ToString("N")+".json");
        var started=DateTimeOffset.UtcNow; var clock=Stopwatch.StartNew();
        using var process=Process.GetCurrentProcess(); var before=process.TotalProcessorTime.TotalSeconds;
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(policy.Seconds));
        var tasks=new List<Task>(); long bytes=0; byte[]? memory=null;
        if (policy.Mode is "cpu" or "peak" or "mixed")
            for(int t=0;t<policy.Threads;t++) tasks.Add(Task.Run(()=>
            { double x=1; while(!stop.IsCancellationRequested && (policy.Mode!="peak" || clock.Elapsed.TotalSeconds<1)) {for(int i=0;i<10000;i++)x=Math.Sqrt(x+123.456); } GC.KeepAlive(x); }));
        if (policy.Mode=="memory") {memory=new byte[policy.MemoryMiB*1024*1024];for(int i=0;i<memory.Length;i+=4096)memory[i]=1;}
        if (policy.Mode.Contains("read") || policy.Mode.Contains("write") || policy.Mode=="mixed")
            tasks.Add(Task.Run(()=>
            {
                var path=Path.Combine(root,"synthetic-"+Guid.NewGuid().ToString("N")+".bin");
                using var file=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.RandomAccess);
                var block=new byte[64*1024]; Random.Shared.NextBytes(block);
                for(int i=0;i<512 && !stop.IsCancellationRequested;i++)file.Write(block); file.Flush(true);
                var write=policy.Mode.Contains("write") || policy.Mode=="mixed"; var random=policy.Mode.StartsWith("random");
                while(!stop.IsCancellationRequested && bytes<128L*1024*1024)
                {
                    file.Position=random ? Random.Shared.Next(0,512)*block.Length : bytes%(32L*1024*1024);
                    if(write) {file.Write(block);bytes+=block.Length;} else bytes+=file.Read(block);
                }
                if(write)file.Flush(true);
            }));
        await Task.WhenAll(tasks);
        if(clock.Elapsed.TotalSeconds<policy.Seconds)await Task.Delay(TimeSpan.FromSeconds(policy.Seconds)-clock.Elapsed);
        GC.KeepAlive(memory); process.Refresh();
        var result=new { policy,startedUtc=started,endedUtc=DateTimeOffset.UtcNow,elapsedSeconds=clock.Elapsed.TotalSeconds,
            cpuSeconds=process.TotalProcessorTime.TotalSeconds-before,workingSetBytes=process.WorkingSet64,ioBytes=bytes,
            groundTruth="Expected workload; requested activity does not prove whole-system saturation or uncached physical I/O." };
        await using var output=new FileStream(resultPath,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        await JsonSerializer.SerializeAsync(output,result,new JsonSerializerOptions{WriteIndented=true});
        Console.WriteLine("CONTROLLED_WORKLOAD_COMPLETED " + resultPath); return 0;
    }
}
