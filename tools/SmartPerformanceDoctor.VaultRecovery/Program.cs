using System.Security.Principal;
using System.Text;
using SmartPerformanceDoctor.App.Services;

if (args.Length != 2 || Console.IsInputRedirected) return 2;
if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) return 3;
Console.Write("Vault password: ");
var password = new StringBuilder();
while (true)
{
    var key = Console.ReadKey(intercept: true);
    if (key.Key == ConsoleKey.Enter) break;
    if (key.Key == ConsoleKey.Backspace && password.Length > 0) password.Length--;
    else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
}
Console.WriteLine();
try
{
    var count = VaultSnapshotRecovery.Export(args[0], args[1], password.ToString());
    Console.WriteLine($"EXPORTED={count}; SOURCE=READ-ONLY; FORMAT=LAB-V4-V5");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name + ": " + ex.Message); return 4; }
finally { password.Clear(); }
