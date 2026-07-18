using System.IO;
using System.Windows;

namespace SmartPerformanceDoctor.Setup;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            WriteSetupDiagnostic("unhandled", args.Exception);
            MessageBox.Show(
                args.Exception.Message + "\n\n설치 파일을 다시 다운로드하거나 관리자 권한으로 실행해 보세요.",
                "설치 프로그램 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(-1);
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            EmbeddedInstallerPayload.EnsureExtracted();
        }
        catch (Exception ex)
        {
            WriteSetupDiagnostic("payload", ex);
            MessageBox.Show(
                ex.Message + "\n\n설치 파일을 다시 다운로드하거나 관리자 권한으로 실행해 보세요.",
                "설치 파일 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        base.OnStartup(e);
    }

    private static void WriteSetupDiagnostic(string category, Exception ex)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PCCare",
                "logs");
            Directory.CreateDirectory(logDir);
            var logPath = Path.Combine(logDir, "setup-startup.log");
            File.AppendAllText(
                logPath,
                $"[{DateTimeOffset.Now:O}] {category}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Best effort only.
        }
    }
}