using SmartPerformanceDoctor.RecoverySandbox;
using System.Text.Json;

try
{
    if (args is ["create", var parent]) { Console.WriteLine(SandboxRecovery.Create(parent).Root); return 0; }
    if (args.Length is < 2 or > 3) return 2;
    var sandbox = SandboxRecovery.Open(args[1]);
    void Checkpoint(string state)
    {
        if (args.Length == 3 && args[2] == state)
        {
            File.WriteAllText(Path.Combine(sandbox.Root, "checkpoint.txt"), state);
            Thread.Sleep(Timeout.Infinite);
        }
    }
    var result = args[0] switch
    {
        "execute" => sandbox.Execute(sandbox.Approve(), checkpoint: Checkpoint),
        "recover" => sandbox.Recover(Checkpoint),
        _ => throw new ArgumentException("UnsupportedCommand")
    };
    Console.WriteLine(JsonSerializer.Serialize(result)); return 0;
}
catch (Exception e) { Console.Error.WriteLine("SANDBOX_BLOCKED " + e.GetType().Name); return 3; }
