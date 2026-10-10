using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Pickers;
using SmartPerformanceDoctor.App.Services.Update;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.UpdateEngine;

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
        var recovery = UpdateRecoveryCoordinator.Inspect();
        if (recovery.State is UpdateRecoveryState.RecoveryAvailable or UpdateRecoveryState.RecoveryRequired or
            UpdateRecoveryState.RecoveryInProgress or UpdateRecoveryState.RecoveryFailed)
        {
            PendingUpdatePanel.Visibility = Visibility.Visible;
            PendingResumeButton.IsEnabled = recovery.State != UpdateRecoveryState.RecoveryInProgress;
            PendingResumeButton.Content = "복구 시작";
            PendingUpdateMessage.Text = recovery.State switch
            {
                UpdateRecoveryState.RecoveryAvailable => "중단된 업데이트를 발견했습니다. 관리자 권한으로 기존 설치 파일을 복구하세요.",
                UpdateRecoveryState.RecoveryInProgress => "업데이트 또는 복구 작업이 진행 중입니다.",
                UpdateRecoveryState.RecoveryFailed => "이전 복구가 실패했습니다. 원본과 증거를 보존했습니다. 관리자 권한으로 다시 복구하세요.",
                _ => $"복구 상태를 확인해야 합니다 ({recovery.Code}). 관리자 권한으로 검증 및 복구하세요."
            };
            return;
        }
        if (recovery.State == UpdateRecoveryState.RecoveryCompleted)
        {
            PendingUpdatePanel.Visibility = Visibility.Visible;
            PendingResumeButton.IsEnabled = false;
            PendingResumeButton.Content = "복구 완료";
            PendingUpdateMessage.Text = "중단된 업데이트의 기존 설치 파일 복원이 완료됐습니다.";
            return;
        }
        var legacyPending = File.Exists(UpdatePaths.PendingState);
        PendingUpdatePanel.Visibility = legacyPending ? Visibility.Visible : Visibility.Collapsed;
        PendingResumeButton.IsEnabled = false;
        PendingResumeButton.Content = "업데이트 마무리";
        if (legacyPending)
            PendingUpdateMessage.Text = "이전 업데이트 대기 기록은 발행자 인증이 없어 앱에서 다시 적용할 수 없습니다. 공식 설치 파일로 업데이트해 주세요.";
    }

    private void ResumePendingUpdate(object sender, RoutedEventArgs e)
    {
        var recovery = UpdateRecoveryCoordinator.Inspect();
        if (recovery.State is UpdateRecoveryState.RecoveryAvailable or UpdateRecoveryState.RecoveryRequired or
            UpdateRecoveryState.RecoveryFailed)
        {
            var launched = UpdateRecoveryCoordinator.LaunchElevatedRecovery();
            PendingUpdateMessage.Text = launched switch
            {
                RecoveryLaunchResult.Launched => "관리자 복구 도우미가 시작됐습니다. 앱을 종료하고 복구 결과를 기다립니다.",
                RecoveryLaunchResult.Cancelled => "관리자 권한 요청이 취소됐습니다. 업데이트는 복구 대기 상태입니다.",
                RecoveryLaunchResult.HelperMissing => "복구 도우미가 설치되지 않았습니다. 공식 설치 파일로 복구하세요.",
                _ => "복구 도우미를 시작하지 못했습니다. 업데이트는 복구 대기 상태입니다."
            };
            if (launched == RecoveryLaunchResult.Launched) App.ExitForUpdateHandoff();
            return;
        }
        if (!File.Exists(UpdatePaths.PendingState))
        {
            RefreshPendingUpdatePanel();
            return;
        }

        PendingUpdateMessage.Text = "이전 업데이트 대기 기록은 발행자 인증이 없어 앱에서 다시 적용할 수 없습니다. 공식 설치 파일로 업데이트해 주세요.";
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
        CheckForUpdateButton.Content = "확인 중…";
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
            CheckForUpdateButton.Content = "업데이트 확인";
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
            Text = "다운로드 후 파일 무결성과 발행자 서명을 검증합니다. 검증이 통과하면 관리자 권한 승인을 요청합니다.",
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
            Text = "다운로드  ·  발행자 검증  ·  관리자 승인  ·  설치",
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
