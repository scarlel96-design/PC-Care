using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App;

public partial class App : Application
{
    private Window? _window;
    private static int _handlingUnhandled;

    public static MainWindow? Shell { get; private set; }

    public App()
    {
        AppLaunchOptions.Parse(Environment.GetCommandLineArgs());
        StartupDiagnostics.Write("app-ctor", AppContext.BaseDirectory);

        // 50.3.0: SecurityLab vault v4 + shred-next product host (disable with PCCARE_SECURITYLAB=0)
        try
        {
            var labOff = string.Equals(
                Environment.GetEnvironmentVariable("PCCARE_SECURITYLAB"),
                "0",
                StringComparison.OrdinalIgnoreCase);
            if (!labOff)
            {
                SmartPerformanceDoctor.SecurityLab.ProductBridge.ProductFeatureFlags.EnableProductHost(
                    master: true,
                    vaultV4: true,
                    shredNext: true,
                    migrate: true);
                StartupDiagnostics.Write("securitylab-host", ProductFeatureFlagsStatus());
            }
            else
            {
                StartupDiagnostics.Write("securitylab-host", "disabled-by-env");
            }
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("securitylab-host-fail", ex.Message);
        }

        // 일반 사용은 비관리자로 실행합니다. 자동 UAC 재실행은 설치/복구 등 명시 요청 시만.
        var requireAdmin = string.Equals(
            Environment.GetEnvironmentVariable("PCCARE_REQUIRE_ADMIN"),
            "1",
            StringComparison.OrdinalIgnoreCase);
        if (requireAdmin && ProcessElevationService.TryRelaunchAsAdministrator())
        {
            StartupDiagnostics.Write("elevation-relaunch", "exiting non-admin instance");
            Environment.Exit(0);
        }

        CrashCaptureService.InstallGlobalHandlers();
        WinUiResourceLayoutGuard.EnsureOrThrow();
        StartupDiagnostics.Write("winui-pri", "ok");

        InitializeComponent();
        StartupDiagnostics.Write("initialize-component", "ok");
        UnhandledException += OnUnhandledException;
    }

    private static string ProductFeatureFlagsStatus() =>
        SmartPerformanceDoctor.SecurityLab.ProductBridge.ProductFeatureFlags.StatusSummary;

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
        StartupDiagnostics.Write("on-launched", "begin");

        // WinUI top-level Window must be created from OnLaunched directly — not inside DispatcherQueue
        // callbacks (timer/enqueue), or the native Window ctor can deadlock / fail-fast.
        try
        {
            StartupDiagnostics.Write("on-launched", "main-window-before");
            Shell = new MainWindow();
            StartupDiagnostics.Write("on-launched", "main-window-after");
            _window = Shell;
            RuntimeIntegrityGuard.EnsureOrExit();
            if (AppLaunchOptions.StartMinimizedToBackground)
            {
                Shell.EnterBackgroundMode();
                StartupDiagnostics.Write("on-launched", "background-mode");
            }
            else
            {
                _window.Activate();
            }

            StartupDiagnostics.Write("on-launched", "ok");
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Write("main-window-fail", ex.Message);
            CrashCaptureService.WriteCrash("main-window-ctor", ex, ex.ToString());
            throw;
        }

        var dispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("DispatcherQueue is not available during OnLaunched.");
        dispatcher.TryEnqueue(RunDeferredStartupWork);
    }

    private void RunDeferredStartupWork()
    {
        var pending = new Services.Update.UpdatePendingApplier().TryApplyOnStartup();
        StartupDiagnostics.Write("on-launched", "update-pending");
        if (!string.IsNullOrWhiteSpace(pending.Message))
        {
            CrashCaptureService.WriteCrash(
                pending.Verified ? "update-pending-applied" : "update-pending-warning",
                null,
                $"Pending update: {pending.FilesApplied} files · {pending.Message}");
            Shell?.SetStatusMessage(pending.Message);
        }

        // Elevated finalize was launched (Program Files write). Exit so the
        // apply script can replace binaries after UAC confirmation.
        if (pending.RequestExit)
        {
            StartupDiagnostics.Write("on-launched", "update-pending-exit-for-elevated-apply");
            try
            {
                Exit();
            }
            catch
            {
                Environment.Exit(0);
            }

            return;
        }

        try
        {
            TrayIconService.Shared.EnsureInitialized();
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("tray-icon-deferred", ex, ex.Message);
        }

        if (AppLaunchOptions.StartMinimizedToBackground)
        {
            TrayIconService.Shared.ShowBalloon(
                AppInfo.ProductName,
                "백그라운드에서 실행 중입니다. 트레이 아이콘을 더블클릭하면 창을 열 수 있습니다.");
        }

        KnowledgeService.Shared.EnsureRulesLoaded();
        StartupDiagnostics.Write("on-launched", "knowledge");
        Services.Commercial.CommercialPackTrustState.Initialize(RuntimePaths.CommercialDataDirectory);
        Services.Commercial.CommercialPackLoader.Shared.EnsureLoaded();
        StartupDiagnostics.Write("on-launched", "commercial");

        new ProgressEventBridgeService().AttachAppLifecycle();
        StartupDiagnostics.Write("on-launched", "progress-bridge");

        QueueStartupTrustNotices();
        ApplyBackgroundAndStartupPreferences();
        _ = RunStartupAegisChecksAsync();
        _ = RunStartupUpdateCheckAsync();
    }

    private static async Task RunStartupUpdateCheckAsync()
    {
        try
        {
            await Services.Update.AutoUpdateCheckService.Shared.RunStartupCheckAsync();
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("auto-update-startup", ex, ex.Message);
        }
    }

    private static void ApplyBackgroundAndStartupPreferences()
    {
        try
        {
            var prefs = BackgroundRunPreferences.Load();
            var exe = InstalledAppPaths.ResolveClientExecutable();
            if (!string.IsNullOrWhiteSpace(exe))
            {
                WindowsStartupRegistration.SetEnabled(prefs.RunAtWindowsStartup, exe);
            }
        }
        catch
        {
            // non-fatal
        }
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

            if (mirrorStatus.RepairedFiles > 0)
            {
                TrayIconService.Shared.EnsureInitialized();
                TrayIconService.Shared.ShowBalloon(
                    AppInfo.ProductName,
                    $"프로그램 파일 {mirrorStatus.RepairedFiles}개를 자동으로 복구했습니다.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }

            SmartPerformanceDoctor.Aegis.AegisLaunchMarker.MarkLaunchSuccess(AppInfo.BuildVersion);

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
