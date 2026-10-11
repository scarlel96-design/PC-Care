using System.Diagnostics;
using System.Text.Json;
using System.Windows.Automation;

if (args.Length != 2 || args[0] != "--exercise-readonly-page")
{
    Console.WriteLine(JsonSerializer.Serialize(new { status="NOT TESTED", reason="Explicit interactive test process and diagnostics page are required.", guiExecuted=false }));
    return 0;
}
var guiExecuted = false;
try
{
    using var process = Process.GetProcessById(int.Parse(args[1]));
    if (!Environment.UserInteractive || process.SessionId != Process.GetCurrentProcess().SessionId || process.MainWindowHandle == IntPtr.Zero)
        throw new InvalidOperationException("InteractiveWindowRequired");
    var window = AutomationElement.FromHandle(process.MainWindowHandle);
    AutomationElement Find(string id) => window.FindFirst(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty,id)) ?? throw new InvalidOperationException("ControlMissing:"+id);
    var start=Find("PerformanceDiagnosticsStart"); var stop=Find("PerformanceDiagnosticsStop");
    var status=Find("PerformanceDiagnosticsStatus"); var plans=Find("PerformanceRecoveryPlans");
    var clock=Stopwatch.StartNew();
    void Wait(Func<bool> condition)
    {
        var limit=Stopwatch.StartNew();
        while(!condition() && limit.Elapsed.TotalSeconds<20)Thread.Sleep(100);
        if(!condition())throw new TimeoutException("GuiStateTimeout");
    }
    for(int repeat=0;repeat<2;repeat++)
    {
        if(!start.Current.IsEnabled)throw new InvalidOperationException("StartDisabled");
        guiExecuted = true;
        start.SetFocus();
        ((InvokePattern)start.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Wait(()=>stop.Current.IsEnabled);
        ((InvokePattern)stop.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Wait(()=>start.Current.IsEnabled && !stop.Current.IsEnabled);
    }
    Console.WriteLine(JsonSerializer.Serialize(new { status="PARTIAL",guiExecuted=true,startStopRestartPass=true,
        statusAccessible=status.Current.Name.Length>0,plansAccessible=plans.Current.AutomationId=="PerformanceRecoveryPlans",
        elapsedMilliseconds=clock.Elapsed.TotalMilliseconds,notTested=new[]{"ScreenReader","HighDpi","ReducedMotion","Navigation","WindowClose","CompletedDryRun"} }));
    return 0;
}
catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{status="FAIL",guiExecuted,error=e.GetType().Name}));return 3;}
