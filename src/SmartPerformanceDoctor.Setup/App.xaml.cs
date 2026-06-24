using System.Windows;

namespace SmartPerformanceDoctor.Setup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EmbeddedInstallerPayload.EnsureExtracted();
        base.OnStartup(e);
    }
}