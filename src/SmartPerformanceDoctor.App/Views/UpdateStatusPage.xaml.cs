using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Update;
using SmartPerformanceDoctor.App.Services.Pickers;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class UpdateStatusPage : Page
{
    private static bool _triggerGitHubCheckOnLoad;

    private readonly UpdateStatusViewModel _viewModel = new();
    private readonly IPathPickerService _pickerService = PathPickerService.Shared;
    private readonly PickerOperationGate _pickerGate = new();

    public static void RequestGitHubCheckOnLoad() => _triggerGitHubCheckOnLoad = true;

    public UpdateStatusPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UiDispatcher.Queue ??= DispatcherQueue.GetForCurrentThread();
        try
        {
            _viewModel.Refresh();
            RefreshPendingUpdatePanel();
            if (_triggerGitHubCheckOnLoad)
            {
                _triggerGitHubCheckOnLoad = false;
                await _viewModel.CheckGitHubAsync();
            }
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("update-page-load", ex, ex.Message);
        }
    }

    private void RefreshStatus(object sender, RoutedEventArgs e)
    {
        _viewModel.Refresh();
        RefreshPendingUpdatePanel();
    }

    private void RefreshPendingUpdatePanel()
    {
        PendingUpdatePanel.Visibility = File.Exists(UpdatePaths.PendingState)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ResumePendingUpdate(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(UpdatePaths.PendingState))
        {
            RefreshPendingUpdatePanel();
            return;
        }

        if (!UpdateInstallerService.LaunchPendingRestart())
        {
            PendingUpdateMessage.Text = "마무리 프로그램을 시작하지 못했습니다. 로그를 확인한 뒤 다시 시도하세요.";
            return;
        }

        PendingUpdateMessage.Text = "업데이트를 마무리합니다. 확인 창이 나타나면 허용해 주세요.";
        Application.Current.Exit();
    }

    private async void PickUpdateFile(object sender, RoutedEventArgs e) => await ExecutePickUpdateFileAsync();

    private async Task ExecutePickUpdateFileAsync()
    {
        if (!_pickerGate.TryEnter())
        {
            return;
        }
        PickUpdateFileButton.IsEnabled = false;
        _viewModel.SetStatus("업데이트 파일 선택 창 여는 중…");
        try
        {
            var result = await _pickerService.PickSingleFileAsync(
                App.Shell,
                new PickerRequest(
                    "UpdateFile",
                    "PC 케어 업데이트 패키지 선택",
                    "업데이트 파일 선택",
                    PickerStartLocation.Downloads,
                    [".spdup", ".zip"]));
            if (result.IsSuccess)
            {
                await _viewModel.SetSelectedPackageAsync(result.Value!);
            }
            else
            {
                _viewModel.SetStatus(result.UserMessage);
            }
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("update-pick-file-local", null, $"Type: {ex.GetType().FullName} · HRESULT: 0x{ex.HResult:X8}");
            _viewModel.SetStatus($"업데이트 파일 선택 처리 중 오류가 발생했습니다. 오류 코드: 0x{ex.HResult:X8}");
        }
        finally
        {
            _pickerGate.Exit();
            PickUpdateFileButton.IsEnabled = true;
            PickUpdateFileButton.Focus(FocusState.Programmatic);
        }
    }
    private async void CheckGitHubRelease(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
        }

        try
        {
            await _viewModel.CheckGitHubAsync();
        }
        finally
        {
            if (button is not null)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void DownloadGitHubUpdate(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
        }

        try
        {
            await _viewModel.DownloadGitHubUpdateAsync();
        }
        finally
        {
            if (button is not null)
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void ApplyUpdate(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
        }

        try
        {
            await _viewModel.ApplySelectedAsync();
        }
        finally
        {
            if (button is not null)
            {
                button.IsEnabled = true;
            }
        }
    }
}