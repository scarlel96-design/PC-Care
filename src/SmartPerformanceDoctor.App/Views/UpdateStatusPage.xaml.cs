using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Pickers;
using SmartPerformanceDoctor.App.Services.Update;
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
                await CheckForUpdateFlowAsync();
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
        App.ExitForUpdateHandoff();
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

    private async void CheckForUpdate(object sender, RoutedEventArgs e) => await CheckForUpdateFlowAsync();

    private async Task CheckForUpdateFlowAsync()
    {
        if (_viewModel.IsBusy)
        {
            return;
        }

        CheckForUpdateButton.IsEnabled = false;
        try
        {
            await _viewModel.CheckGitHubAsync();
            var update = _viewModel.AvailableGitHubUpdate;
            if (!_viewModel.IsGitHubUpdateAvailable || update is null)
            {
                return;
            }

            if (await ShowUpdateOfferAsync(update))
            {
                await _viewModel.DownloadAndApplyGitHubUpdateAsync();
            }
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("update-one-click-flow", ex, ex.Message);
            _viewModel.SetStatus("업데이트 처리 중 오류가 발생했습니다. 잠시 후 다시 시도하세요.");
        }
        finally
        {
            CheckForUpdateButton.IsEnabled = true;
        }
    }

    private async Task<bool> ShowUpdateOfferAsync(RemoteUpdateCheckResult update)
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 420 };
        content.Children.Add(new TextBlock
        {
            Text = $"{_viewModel.CurrentVersion}  →  {update.LatestVersion}",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = "지금 업데이트를 누르면 다운로드 후 SHA-256 무결성을 확인하고 자동으로 적용합니다.",
            TextWrapping = TextWrapping.Wrap
        });

        if (update.ReleaseNotesLines.Count > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = "이번 업데이트",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 0)
            });

            var notes = new StackPanel { Spacing = 6 };
            foreach (var note in update.ReleaseNotesLines.Take(6))
            {
                notes.Children.Add(new TextBlock
                {
                    Text = $"• {note}",
                    TextWrapping = TextWrapping.Wrap
                });
            }

            content.Children.Add(new ScrollViewer
            {
                Content = notes,
                MaxHeight = 180,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = "1 다운로드  ·  2 보안 검증  ·  3 자동 적용",
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SpdMutedTextBrush"],
            Margin = new Thickness(0, 6, 0, 0)
        });

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "새 업데이트를 사용할 수 있습니다",
            Content = content,
            PrimaryButtonText = "지금 업데이트",
            CloseButtonText = "나중에",
            DefaultButton = ContentDialogButton.Primary
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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