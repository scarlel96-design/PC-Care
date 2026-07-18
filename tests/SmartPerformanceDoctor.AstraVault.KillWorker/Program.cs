using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

if (args.Length < 3)
{
    Environment.Exit(2);
}

var root = args[0];
var marker = (Av3FaultPoint)int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
var planPath = args[2];

Av3KillChildWorkerEntry.Run(root, marker, planPath);