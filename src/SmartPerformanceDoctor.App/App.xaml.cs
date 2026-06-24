using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App;

public partial class App : Application
{
    private Window? _window;
    private static int _handlingUnhandled;

    public static MainWindow? Shell { get; private set; }

    public App()
    {
        if (ProcessElevationService.TryRelaunchAsAdministrator())
        {
            Environment.Exit(0);
        }

        RuntimeIntegrityGuard.EnsureOrExit();
        CrashCaptureService.InstallGlobalHandlers();
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _handlingUnhandled, 1, 0) != 0)
        {
            e.Handled = true;
            return;
        }

        try
        {
            CrashCaptureService.WriteCrash("winui-unhandled", e.Exception, e.Exception.ToString());
            try
            {
                SmartPerformanceDoctor.Aegis.AegisLaunchMarker.MarkLaunchFailure(AppInfo.BuildVersion, e.Exception.Message);
            }
            catch
            {
                // MarkLaunchFailure must not recurse through EnsureLayout failures.
            }
        }
        finally
        {
            Interlocked.Exchange(ref _handlingUnhandled, 0);
        }

        e.Handled = true;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var pending = new Services.Update.UpdatePendingApplier().TryApplyOnStartup();
        if (!string.IsNullOrWhiteSpace(pending.Message))
        {
            CrashCaptureService.WriteCrash(
                pending.Verified ? "update-pending-applied" : "update-pending-warning",
                null,
                $"Pending update: {pending.FilesApplied} files · {pending.Message}");
        }

        KnowledgeService.Shared.EnsureRulesLoaded();
        Services.Commercial.CommercialPackTrustState.Initialize(RuntimePaths.CommercialDataDirectory);
        Services.Commercial.CommercialPackLoader.Shared.EnsureLoaded();

        new ProgressEventBridgeService().AttachAppLifecycle();
        Shell = new MainWindow();
        _window = Shell;
        _window.Activate();
        QueueStartupTrustNotices();
        _ = RunStartupAegisChecksAsync();
    }

    private static async Task RunStartupAegisChecksAsync()
    {
        try
        {
            var mirrorStatus = await Task.Run(() =>
            {
                if (SmartPerformanceDoctor.Aegis.AegisLaunchMarker.RequiresPreLaunchRepair())
                {
                    Services.Aegis.AegisMirrorService.Shared.RunManualCheck(AppInfo.BuildVersion, attemptRepair: true);
                }

                return Services.Aegis.AegisMirrorService.Shared.RunStartupCheck(AppInfo.BuildVersion);
            });

            if (mirrorStatus.SafeModeActive)
            {
                CrashCaptureService.WriteCrash("aegis-safe-mode", null, mirrorStatus.SafeModeReason + ": " + mirrorStatus.Message);
            }

            if (mirrorStatus.RepairAttempted || mirrorStatus.IntegrityFailures > 0)
            {
                CrashCaptureService.WriteCrash(
                    mirrorStatus.IntegrityFailures == 0 ? "aegis-mirror-repair" : "aegis-mirror-warning",
                    null,
                    $"{mirrorStatus.Message} · failures={mirrorStatus.IntegrityFailures} · repaired={mirrorStatus.RepairedFiles}");
            }
            else
            {
                SmartPerformanceDoctor.Aegis.AegisLaunchMarker.MarkLaunchSuccess(AppInfo.BuildVersion);
            }

            Services.Aegis.AegisProtectionBackgroundService.Shared.Start(AppInfo.BuildVersion);
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("aegis-startup-check", ex, ex.Message);
            try
            {
                Services.Aegis.AegisProtectionBackgroundService.Shared.Start(AppInfo.BuildVersion);
            }
            catch
            {
                // Background protection must not block startup.
            }
        }
    }

    private void QueueStartupTrustNotices()
    {
        var notices = StartupTrustStatusService.BuildStartupNotices();
        if (notices.Count == 0 || Shell is null)
        {
            return;
        }

        Shell.DispatcherQueue.TryEnqueue(async () =>
        {
            foreach (var notice in notices)
            {
                var dialog = new ContentDialog
                {
                    Title = AppInfo.ProductName,
                    Content = new TextBlock
                    {
                        Text = notice,
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = "확인",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Shell.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
        });
    }
}
