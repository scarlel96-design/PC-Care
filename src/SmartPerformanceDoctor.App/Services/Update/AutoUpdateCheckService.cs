using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Views;

namespace SmartPerformanceDoctor.App.Services.Update;

/// <summary>시작 시 원격 업데이트를 확인하고 새 버전이 있으면 팝업·트레이 알림을 표시합니다.</summary>
public sealed class AutoUpdateCheckService
{
    public static AutoUpdateCheckService Shared { get; } = new();

    private readonly object _gate = new();
    private RemoteUpdateCheckResult? _pendingResult;
    private bool _dialogOpen;

    private AutoUpdateCheckService()
    {
    }

    public async Task RunStartupCheckAsync(CancellationToken cancellationToken = default)
    {
        if (IsSkippedByEnvironment())
        {
            return;
        }

        var prefs = AutoUpdateCheckPreferences.Load();
        if (!prefs.Enabled || !prefs.ShouldCheckNow())
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

        try
        {
            using var github = new GitHubReleaseUpdateService();
            var result = await github.CheckLatestAsync(cancellationToken).ConfigureAwait(false);

            prefs.RecordCheck();
            prefs.Save();

            if (!IsUpdateAvailable(result) || prefs.IsDismissed(result.LatestVersion))
            {
                return;
            }

            lock (_gate)
            {
                _pendingResult = result;
            }

            await NotifyUpdateAvailableAsync(result).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("auto-update-check", ex, ex.Message);
        }
    }

    public void TryShowPendingPopup()
    {
        RemoteUpdateCheckResult? pending;
        lock (_gate)
        {
            pending = _pendingResult;
        }

        if (pending is null || !IsUpdateAvailable(pending))
        {
            return;
        }

        var prefs = AutoUpdateCheckPreferences.Load();
        if (prefs.IsDismissed(pending.LatestVersion))
        {
            lock (_gate)
            {
                _pendingResult = null;
            }

            return;
        }

        UiDispatcher.Run(() => _ = ShowPopupAsync(pending));
    }

    public static bool IsUpdateAvailable(RemoteUpdateCheckResult check) =>
        check.Success
        && UpdateVersionComparer.IsNewer(check.LatestVersion, AppInfo.Version)
        && !string.IsNullOrWhiteSpace(check.UpdateDownloadUrl);

    private static bool IsSkippedByEnvironment() =>
        string.Equals(
            Environment.GetEnvironmentVariable("PCCARE_SKIP_UPDATE_CHECK"),
            "1",
            StringComparison.OrdinalIgnoreCase);

    private Task NotifyUpdateAvailableAsync(RemoteUpdateCheckResult check)
    {
        if (AppLaunchOptions.StartMinimizedToBackground)
        {
            TrayIconService.Shared.EnsureInitialized();
            TrayIconService.Shared.ShowBalloon(
                AppInfo.ProductName,
                $"새 버전 {check.LatestVersion}이 있습니다. 트레이 아이콘을 눌러 업데이트하세요.",
                System.Windows.Forms.ToolTipIcon.Info);
            return Task.CompletedTask;
        }

        return UiDispatcher.RunAsync(() => ShowPopupAsync(check));
    }

    private async Task ShowPopupAsync(RemoteUpdateCheckResult check)
    {
        if (_dialogOpen || App.Shell?.Content.XamlRoot is null)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            var notes = check.ReleaseNotesLines.Take(6).ToArray();
            var notesBlock = notes.Length > 0
                ? string.Join(Environment.NewLine, notes.Select(n => $"• {n}"))
                : "릴리즈 노트가 없습니다.";

            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(new TextBlock
            {
                Text = $"현재 {AppInfo.Version} → 새 버전 {check.LatestVersion}",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = notesBlock,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.9
            });

            var dialog = new ContentDialog
            {
                Title = "업데이트 사용 가능",
                Content = panel,
                PrimaryButtonText = "업데이트 화면으로",
                CloseButtonText = "나중에",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = App.Shell.Content.XamlRoot
            };

            var choice = await dialog.ShowAsync();
            if (choice == ContentDialogResult.Primary)
            {
                lock (_gate)
                {
                    _pendingResult = null;
                }

                OpenUpdatePageWithCheck();
                return;
            }

            var prefs = AutoUpdateCheckPreferences.Load();
            prefs.DismissVersion(check.LatestVersion);
            prefs.Save();
            lock (_gate)
            {
                _pendingResult = null;
            }
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private static void OpenUpdatePageWithCheck()
    {
        if (App.Shell?.NavigationFrame is not { } frame)
        {
            return;
        }

        UpdateStatusPage.RequestGitHubCheckOnLoad();
        AppNavigationService.TryNavigateByName(frame, "UpdateStatusPage");
        App.Shell.ShowFromTray();
    }
}