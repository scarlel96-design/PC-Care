using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Input;
using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.Services.Pickers;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SecureVaultCenterPage : Page
{
    private readonly SecureVaultViewModel _viewModel = new();
    private readonly IPathPickerService _pickerService = PathPickerService.Shared;
    private readonly DispatcherTimer _autoLockTimer;
    private readonly DispatcherTimer _sessionCountdownTimer;
    private bool _operationInProgress;
    private readonly PickerOperationGate _pickerGate = new();

    public SecureVaultCenterPage()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"보안 금고 화면을 불러오지 못했습니다: {ex.Message}", ex);
        }

        _autoLockTimer = new DispatcherTimer();
        _autoLockTimer.Tick += OnAutoLockTick;
        _sessionCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _sessionCountdownTimer.Tick += OnSessionCountdownTick;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.AutoLockRequested += OnAutoLockActivity;
        Loaded += OnPageLoaded;
        Unloaded += OnUnloaded;
        InitializeAutoLockMinutesBox();
        EntryList.ItemsSource = _viewModel.VisibleItems;
        SyncBoundText();
        UpdatePanelVisibility();
        UpdateSelectionSummary();
    }

    private void InitializeAutoLockMinutesBox()
    {
        AutoLockMinutesBox.Items.Clear();
        for (var minutes = 1; minutes <= 60; minutes++)
        {
            AutoLockMinutesBox.Items.Add(minutes);
        }

        AutoLockMinutesBox.SelectedItem = _viewModel.AutoLockMinutes;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        RefreshFromNavigation();
    }

    public void RefreshFromNavigation()
    {
        try
        {
            _viewModel.RefreshState();
            UpdatePanelVisibility();
        }
        catch (Exception ex)
        {
            _viewModel.SetStatus($"금고 화면을 불러오지 못했습니다: {ex.Message}");
            CreatePanel.Visibility = Visibility.Visible;
            UnlockPanel.Visibility = Visibility.Collapsed;
            UnlockedPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e) => RefreshFromNavigation();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _autoLockTimer.Stop();
        _sessionCountdownTimer.Stop();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.AutoLockRequested -= OnAutoLockActivity;
    }

    private void OnAutoLockTick(object? sender, object e)
    {
        if (!_viewModel.IsUnlocked)
        {
            _autoLockTimer.Stop();
            _sessionCountdownTimer.Stop();
            return;
        }

        _viewModel.Lock();
        _viewModel.SetStatus($"비활성 {_viewModel.AutoLockMinutes}분 경과 — 금고를 자동 잠갔습니다.");
        _autoLockTimer.Stop();
        _sessionCountdownTimer.Stop();
    }

    private void OnSessionCountdownTick(object? sender, object e)
    {
        if (!_viewModel.IsUnlocked)
        {
            _sessionCountdownTimer.Stop();
            SessionCountdownText.Text = "";
            return;
        }

        _viewModel.RefreshSessionCountdown();
        if (SessionCountdownText is not null)
        {
            SessionCountdownText.Text = string.IsNullOrWhiteSpace(_viewModel.SessionCountdownLine)
                ? ""
                : "세션: " + _viewModel.SessionCountdownLine;
        }

        if (SecurityStateText is not null && !string.IsNullOrWhiteSpace(_viewModel.SecurityStateLine))
        {
            SecurityStateText.Text = "보안 상태: " + _viewModel.SecurityStateLine;
        }
    }

    private void OnAutoLockActivity(object? sender, EventArgs e) => ResetAutoLockTimer();

    private void ResetAutoLockTimer()
    {
        _autoLockTimer.Stop();
        if (_viewModel.IsUnlocked)
        {
            _autoLockTimer.Interval = TimeSpan.FromMinutes(_viewModel.AutoLockMinutes);
            _autoLockTimer.Start();
            if (!_sessionCountdownTimer.IsEnabled)
            {
                _sessionCountdownTimer.Start();
            }

            _viewModel.RefreshSessionCountdown();
        }
        else
        {
            _sessionCountdownTimer.Stop();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SecureVaultViewModel.StateLine)
            or nameof(SecureVaultViewModel.StatusLine)
            or nameof(SecureVaultViewModel.CryptoLine)
            or nameof(SecureVaultViewModel.SecurityLine)
            or nameof(SecureVaultViewModel.SecurityStateLine)
            or nameof(SecureVaultViewModel.SessionCountdownLine)
            or nameof(SecureVaultViewModel.Breadcrumb)
            or nameof(SecureVaultViewModel.VisibleItems)
            or null
            or "")
        {
            SyncBoundText();
        }

        if (e.PropertyName is nameof(SecureVaultViewModel.VisibleItems) or null or "")
        {
            RefreshEntryList(clearSelection: true);
        }

        if (e.PropertyName is nameof(SecureVaultViewModel.IsNotCreated)
            or nameof(SecureVaultViewModel.IsLocked)
            or nameof(SecureVaultViewModel.IsUnlocked))
        {
            UpdatePanelVisibility();
            if (_viewModel.IsUnlocked)
            {
                ResetAutoLockTimer();
            }
            else
            {
                _autoLockTimer.Stop();
                _sessionCountdownTimer.Stop();
            }
        }

        if (e.PropertyName is nameof(SecureVaultViewModel.CanNavigateBack))
        {
            BackButton.Visibility = _viewModel.CanNavigateBack ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdatePanelVisibility()
    {
        CreatePanel.Visibility = _viewModel.IsNotCreated ? Visibility.Visible : Visibility.Collapsed;
        UnlockPanel.Visibility = _viewModel.IsLocked ? Visibility.Visible : Visibility.Collapsed;
        UnlockedPanel.Visibility = _viewModel.IsUnlocked ? Visibility.Visible : Visibility.Collapsed;
        BackButton.Visibility = _viewModel.CanNavigateBack ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncBoundText()
    {
        StateLineText.Text = _viewModel.StateLine;
        StatusLineText.Text = _viewModel.StatusLine;
        CryptoLineText.Text = _viewModel.CryptoLine;
        SecurityLineText.Text = _viewModel.SecurityLine;
        if (SecurityStateText is not null)
        {
            SecurityStateText.Text = string.IsNullOrWhiteSpace(_viewModel.SecurityStateLine)
                ? ""
                : "보안 상태: " + _viewModel.SecurityStateLine;
        }

        if (SessionCountdownText is not null)
        {
            SessionCountdownText.Text = string.IsNullOrWhiteSpace(_viewModel.SessionCountdownLine)
                ? ""
                : "세션: " + _viewModel.SessionCountdownLine;
        }

        BreadcrumbText.Text = _viewModel.Breadcrumb;
    }

    // Keep the observable collection connected. Resetting ItemsSource to null for every
    // status update recreated all rows and produced a visible flash in the vault list.
    private void RefreshEntryList(bool clearSelection = false)
    {
        if (!ReferenceEquals(EntryList.ItemsSource, _viewModel.VisibleItems))
        {
            EntryList.ItemsSource = _viewModel.VisibleItems;
        }

        if (clearSelection)
        {
            EntryList.SelectedItems.Clear();
            UpdateSelectionSummary();
        }
    }

    private async Task ShowOperationResultDialogAsync(string title, SecureVaultOperationResult result)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = result.Message,
                TextWrapping = TextWrapping.Wrap
            },
            CloseButtonText = "확인",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void CreateVault(object sender, RoutedEventArgs e)
    {
        var password = CreatePasswordBox.Password;
        var confirm = CreatePasswordConfirmBox.Password;
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
        {
            _viewModel.SetStatus("비밀번호가 일치하지 않습니다.");
            return;
        }

        var hint = string.IsNullOrWhiteSpace(RecoveryHintBox.Text) ? null : RecoveryHintBox.Text.Trim();
        var result = _viewModel.CreateVault(password, hint);
        if (result.Success)
        {
            if (result.RecoveryCodes is { Count: > 0 })
            {
                var codes = string.Join(Environment.NewLine, result.RecoveryCodes);
                _ = ShowRecoveryKeyDialogAsync(
                    "금고 v4 생성 완료 · 복구 코드",
                    "아래 복구 코드 10개를 안전한 곳에 보관하세요. 각 코드는 일회용이며, 디스크에는 해시만 저장됩니다.\n" +
                    "잠금 해제는 비밀번호로 하세요.\n\n" + codes);
            }
            else if (!string.IsNullOrWhiteSpace(result.RecoveryKey))
            {
                _ = ShowRecoveryKeyDialogAsync("금고 생성 완료", result.RecoveryKey);
            }
        }

        ClearPasswordBoxes();
    }

    private void UnlockWithRecoveryKey(object sender, RoutedEventArgs e)
    {
        _viewModel.UnlockWithRecoveryKey(RecoveryKeyBox.Text);
        RecoveryKeyBox.Text = "";
    }

    private async void ChangePassword(object sender, RoutedEventArgs e)
    {
        var currentBox = new PasswordBox { PlaceholderText = "현재 비밀번호" };
        var newBox = new PasswordBox { PlaceholderText = "새 비밀번호 (12자+)" };
        var confirmBox = new PasswordBox { PlaceholderText = "새 비밀번호 확인" };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(currentBox);
        panel.Children.Add(newBox);
        panel.Children.Add(confirmBox);

        var dialog = new ContentDialog
        {
            Title = "마스터 비밀번호 변경",
            Content = panel,
            PrimaryButtonText = "변경",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (!string.Equals(newBox.Password, confirmBox.Password, StringComparison.Ordinal))
        {
            _viewModel.SetStatus("새 비밀번호가 일치하지 않습니다.");
            return;
        }

        var result = _viewModel.ChangeMasterPassword(currentBox.Password, newBox.Password);
        if (result.Success && result.RecoveryCodes is { Count: > 0 })
        {
            await ShowRecoveryKeyDialogAsync(
                "비밀번호 변경 완료 · 새 복구 코드",
                "비밀번호가 변경되었습니다. 이전 복구 코드는 모두 무효입니다.\n"
                + "새 복구 코드 10개를 안전한 곳에 보관하세요.\n\n"
                + string.Join(Environment.NewLine, result.RecoveryCodes));
        }
        else if (result.Success && !string.IsNullOrWhiteSpace(result.RecoveryKey))
        {
            await ShowRecoveryKeyDialogAsync("비밀번호 변경 완료", result.RecoveryKey);
        }
        else if (!result.Success)
        {
            _viewModel.SetStatus(result.Message);
        }
    }

    private async void ReissueRecoveryCodes(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsLabVaultFormat)
        {
            _viewModel.SetStatus("복구 코드 재발급은 v5 Lab 금고에서만 지원됩니다.");
            return;
        }

        var passwordBox = new PasswordBox { PlaceholderText = "현재 마스터 비밀번호 확인" };
        var dialog = new ContentDialog
        {
            Title = "복구 코드 재발급",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "이전 일회용 복구 코드는 모두 무효가 됩니다. 새 코드 10개가 발급됩니다.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    passwordBox
                }
            },
            PrimaryButtonText = "재발급",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = _viewModel.ReissueRecoveryCodes(passwordBox.Password);
        if (result.Success && result.RecoveryCodes is { Count: > 0 })
        {
            await ShowRecoveryKeyDialogAsync(
                "복구 코드 재발급 완료",
                "이전 코드는 모두 무효입니다. 새 복구 코드 10개를 보관하세요.\n\n"
                + string.Join(Environment.NewLine, result.RecoveryCodes));
        }
        else
        {
            _viewModel.SetStatus(result.Message);
        }
    }

    private void AutoLockMinutesChanged(object sender, SelectionChangedEventArgs args)
    {
        if (AutoLockMinutesBox.SelectedItem is not int minutes)
        {
            return;
        }

        _viewModel.AutoLockMinutes = minutes;
        ResetAutoLockTimer();
    }

    private async Task ShowRecoveryKeyDialogAsync(string title, string recoveryKey)
    {
        var body = recoveryKey.Contains("복구 코드", StringComparison.Ordinal)
            || recoveryKey.Contains('\n')
            ? recoveryKey
            : $"아래 복구 키를 안전한 곳에 보관하세요. 분실 시 비밀번호 없이 금고를 열 수 없습니다.\n\n{recoveryKey}";

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                },
                MaxHeight = 420
            },
            PrimaryButtonText = "확인 · 보관 완료",
            CloseButtonText = "닫기",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void MigrateToLabV4(object sender, RoutedEventArgs e)
    {
        var passwordBox = new PasswordBox { PlaceholderText = "현재 금고 비밀번호" };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "기존 v3 금고를 재암호화해 Lab v4로 가져옵니다. 원본 v3는 삭제하지 않습니다. 결과는 secure_vault\\v4-migrated 경로에 생성됩니다.",
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(passwordBox);

        var dialog = new ContentDialog
        {
            Title = "v3 → v4 마이그레이션",
            Content = panel,
            PrimaryButtonText = "마이그레이션 실행",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = _viewModel.MigrateLegacyToLabV4(passwordBox.Password);
        await ShowOperationResultDialogAsync("마이그레이션", result);
    }

    private void UnlockVault(object sender, RoutedEventArgs e)
    {
        _viewModel.Unlock(UnlockPasswordBox.Password);
        UnlockPasswordBox.Password = "";
    }

    private void UnlockReadOnly(object sender, RoutedEventArgs e)
    {
        var result = _viewModel.UnlockReadOnly(UnlockPasswordBox.Password);
        UnlockPasswordBox.Password = "";
        if (!result.Success)
        {
            _viewModel.SetStatus(result.Message);
        }
    }

    private void LockVault(object sender, RoutedEventArgs e) => _viewModel.Lock();

    private async void AddFile(object sender, RoutedEventArgs e) => await ExecuteAddFileAsync();

    private async Task ExecuteAddFileAsync()
    {
        if (!BeginPickerOperation("파일 선택 창 여는 중…"))
        {
            return;
        }

        string? path = null;
        try
        {
            var result = await _pickerService.PickSingleFileAsync(
                App.Shell,
                new PickerRequest("VaultFile", "금고에 넣을 파일 선택", "파일 넣기", FileTypeFilter: ["*"]));
            if (result.IsSuccess)
            {
                var validation = VaultImportPathValidator.ValidateFile(result.Value!);
                if (validation.Allowed)
                {
                    path = validation.NormalizedPath;
                    _viewModel.SetStatus("파일 선택 완료 · 경로 검증을 통과했습니다.");
                }
                else
                {
                    _viewModel.SetStatus($"파일을 추가할 수 없습니다: {validation.Message}");
                }
            }
            else
            {
                _viewModel.SetStatus(result.UserMessage);
            }
        }
        catch (Exception ex)
        {
            _viewModel.SetStatus($"파일 선택 처리 중 오류가 발생했습니다: {ex.Message}");
        }
        finally
        {
            EndPickerOperation(AddFileButton);
        }

        if (path is null)
        {
            return;
        }

        var sealOrigin = SealOriginCheckBox.IsChecked == true;
        var operationResult = await RunVaultOperationWithProgressAsync(
            sealOrigin ? "파일 암호화 보관 · 원본 잠금" : "파일 암호화 보관",
            progress => _viewModel.AddFileAsync(path, progress, sealOrigin));
        await ShowOperationResultDialogAsync(operationResult.Success ? "보관 완료" : "보관 실패", operationResult);
        RefreshEntryList();
    }

    private async void AddFolder(object sender, RoutedEventArgs e) => await ExecuteAddFolderAsync();

    private async Task ExecuteAddFolderAsync()
    {
        if (!BeginPickerOperation("폴더 선택 창 여는 중…"))
        {
            return;
        }

        string? path = null;
        try
        {
            var result = await _pickerService.PickFolderAsync(
                App.Shell,
                new PickerRequest("VaultFolder", "금고에 넣을 폴더 선택", "폴더 넣기"));
            if (result.IsSuccess)
            {
                var validation = VaultImportPathValidator.ValidateDirectory(result.Value!);
                if (validation.Allowed)
                {
                    path = validation.NormalizedPath;
                    _viewModel.SetStatus("폴더 선택 완료 · 재귀 열거는 금고 가져오기 단계에서 처리합니다.");
                }
                else
                {
                    _viewModel.SetStatus($"폴더를 추가할 수 없습니다: {validation.Message}");
                }
            }
            else
            {
                _viewModel.SetStatus(result.UserMessage);
            }
        }
        catch (Exception ex)
        {
            _viewModel.SetStatus($"폴더 선택 처리 중 오류가 발생했습니다: {ex.Message}");
        }
        finally
        {
            EndPickerOperation(AddFolderButton);
        }

        if (path is null)
        {
            return;
        }

        var sealOrigin = SealOriginCheckBox.IsChecked == true;
        var operationResult = await RunVaultOperationWithProgressAsync(
            sealOrigin ? "폴더 암호화 보관 · 원본 잠금" : "폴더 암호화 보관",
            progress => _viewModel.AddFolderAsync(path, progress, sealOrigin));
        await ShowOperationResultDialogAsync(operationResult.Success ? "보관 완료" : "보관 실패", operationResult);
        RefreshEntryList();
    }

    private bool BeginPickerOperation(string status)
    {
        if (_operationInProgress || !_pickerGate.TryEnter())
        {
            return false;
        }

        _viewModel.SetStatus(status);
        SetPickerButtonsEnabled(false);
        return true;
    }

    private void EndPickerOperation(Control focusTarget)
    {
        _pickerGate.Exit();
        SetPickerButtonsEnabled(true);
        focusTarget.Focus(FocusState.Programmatic);
    }

    private void SetPickerButtonsEnabled(bool enabled)
    {
        AddFileButton.IsEnabled = enabled;
        AddFolderButton.IsEnabled = enabled;
        ExportEntryButton.IsEnabled = enabled;
    }
    private void NavigateBack(object sender, RoutedEventArgs e) => _viewModel.NavigateBack();

    private void EntryDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (EntryList.SelectedItem is SecureVaultBrowsableItem item && item.IsNavigable)
        {
            _viewModel.NavigateInto(item);
            e.Handled = true;
        }
    }

    private void OpenSelectedEntry(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is SecureVaultBrowsableItem item && item.IsNavigable)
        {
            _viewModel.NavigateInto(item);
        }
        else
        {
            _viewModel.SetStatus("열 폴더를 선택하세요.");
        }
    }

    private SecureVaultBrowsableItem? GetSelectedItem() =>
        EntryList.SelectedItem as SecureVaultBrowsableItem;

    private IReadOnlyCollection<SecureVaultBrowsableItem> GetSelectedItems() =>
        EntryList.SelectedItems
            .OfType<SecureVaultBrowsableItem>()
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

    private void EntrySelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectionSummary();

    private void SelectAllEntries(object sender, RoutedEventArgs e)
    {
        EntryList.SelectAll();
        UpdateSelectionSummary();
    }

    private void ClearEntrySelection(object sender, RoutedEventArgs e)
    {
        EntryList.SelectedItems.Clear();
        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        if (SelectionSummaryText is null)
        {
            return;
        }

        var selected = EntryList.SelectedItems.Count;
        SelectionSummaryText.Text = selected == 0
            ? "선택된 항목 없음"
            : $"{selected}개 선택 · 한 번에 내보낼 수 있습니다";
    }

    private async Task<string?> PickExportFolderAsync()
    {
        if (!BeginPickerOperation("내보낼 폴더 선택 창 여는 중…"))
        {
            return null;
        }

        try
        {
            var result = await _pickerService.PickFolderAsync(
                App.Shell,
                new PickerRequest("VaultExportFolder", "금고 항목을 내보낼 폴더 선택", "이 위치로 보내기"));
            _viewModel.SetStatus(result.UserMessage);
            return result.IsSuccess ? result.Value : null;
        }
        catch (Exception ex)
        {
            _viewModel.SetStatus($"내보낼 폴더 선택 중 오류가 발생했습니다: {ex.Message}");
            return null;
        }
        finally
        {
            EndPickerOperation(ExportEntryButton);
        }
    }
    private async void ExportEntry(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems();
        if (items.Count == 0)
        {
            _viewModel.SetStatus("내보낼 파일 또는 폴더를 목록에서 하나 이상 선택하세요.");
            return;
        }

        var destination = await PickExportFolderAsync();
        if (destination is null)
        {
            return;
        }

        var result = await _viewModel.ExportEntriesAsync(items, destination, stepUpConfirmed: false);
        if (!result.Success && result.Message.Contains("추가 확인", StringComparison.Ordinal))
        {
            var confirm = new ContentDialog
            {
                Title = "보안 게이트 · step-up",
                Content = $"선택한 {items.Count}개 항목을 내보내려면 추가 확인이 필요합니다. 계속할까요?",
                PrimaryButtonText = "확인 후 내보내기",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                result = await _viewModel.ExportEntriesAsync(items, destination, stepUpConfirmed: true);
            }
        }

        await ShowOperationResultDialogAsync("내보내기", result);
    }
    private async void RestoreToOrigin(object sender, RoutedEventArgs e)
    {
        _viewModel.TouchActivity();
        if (GetSelectedItem() is not { } item)
        {
            _viewModel.SetStatus("복원할 항목을 목록에서 최상위 폴더 또는 파일을 선택하세요.");
            return;
        }

        if (item.Kind == SecureVaultBrowsableKind.SubFolder)
        {
            _viewModel.SetStatus("하위 폴더가 아니라 최상위 폴더를 선택한 뒤 원본 복원을 실행하세요.");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "원본 복원",
            Content = $"「{item.DisplayName}」을(를) 원본 위치에 복원하고 금고에서 제거합니다.\n계속할까요?",
            PrimaryButtonText = "원본 복원",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (_operationInProgress)
        {
            return;
        }

        var result = await RunVaultOperationWithProgressAsync(
            "원본 복원",
            progress => _viewModel.RestoreToOriginAsync(item, progress));
        await ShowOperationResultDialogAsync("원본 복원", result);
        RefreshEntryList();
    }

    private async void RemoveFromVault(object sender, RoutedEventArgs e)
    {
        if (GetSelectedItem() is not { } item)
        {
            _viewModel.SetStatus("삭제할 항목을 선택하세요.");
            return;
        }

        if (!await ConfirmPermanentDeleteAsync(item))
        {
            return;
        }

        _viewModel.RemoveFromVault(item);
    }

    private async Task<bool> ConfirmPermanentDeleteAsync(SecureVaultBrowsableItem item)
    {
        var warning = new TextBlock
        {
            Text =
                $"「{item.DisplayName}」을(를) 보안 삭제합니다. (보안 금고 기능)\n\n" +
                "복원 없이 암호화 데이터가 파기되며, 원본 파일은 되돌릴 수 없습니다.\n" +
                "파일을 되살리려면 「원본 복원」 또는 「다른 위치로 보내기」를 사용하세요.",
            TextWrapping = TextWrapping.Wrap
        };
        var confirmCheck = new CheckBox
        {
            Content = "복원 없이 보안 삭제됨을 이해했으며, 삭제에 동의합니다.",
            Margin = new Thickness(0, 12, 0, 0)
        };
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(warning);
        panel.Children.Add(confirmCheck);

        var dialog = new ContentDialog
        {
            Title = "보안 삭제",
            Content = panel,
            PrimaryButtonText = "보안 삭제",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = XamlRoot
        };

        confirmCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = confirmCheck.IsChecked == true;
        confirmCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void CompactPacks(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsUnlocked)
        {
            _viewModel.SetStatus("금고를 연 뒤 Pack 정리를 실행하세요.");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Pack 정리 (GC)",
            Content =
                "살아 있는 객체만 새 pack으로 재패킹하고 tombstone을 제거합니다.\n" +
                "쓰기 세션이 필요하며, 완료 전 강제 종료 시 pack이 일시적으로 불완전할 수 있습니다.\n계속할까요?",
            PrimaryButtonText = "정리 실행",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = _viewModel.CompactPacks();
        await ShowOperationResultDialogAsync("Pack 정리", result);
    }

    private async void RepairActivation(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsUnlocked)
        {
            _viewModel.SetStatus("금고를 연 뒤 Activation 복구를 실행하세요.");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Activation commit 복구",
            Content =
                "현재 메타데이터·헤더 해시로 activation.commit 마커를 다시 씁니다.\n" +
                "메타가 AEAD로 정상 복호화된 상태에서만 의미가 있습니다.\n계속할까요?",
            PrimaryButtonText = "복구 실행",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = _viewModel.RepairActivation();
        await ShowOperationResultDialogAsync("Activation 복구", result);
    }

    private async void VerifyIntegrity(object sender, RoutedEventArgs e)
    {
        var result = _viewModel.VerifyIntegrity();
        var issueLines = result.Issues
            .Take(8)
            .Select(issue => $"• [{issue.Kind}] {issue.Label} {issue.Detail}".Trim())
            .ToList();
        if (result.Issues.Count > 8)
        {
            issueLines.Add($"• 외 {result.Issues.Count - 8}건");
        }

        var body = result.Message;
        if (issueLines.Count > 0)
        {
            body += "\n\n상세:\n" + string.Join("\n", issueLines);
        }

        body +=
            $"\n\n검증: 매니페스트 {(result.ManifestIntegrityValid ? "정상" : "오류")} · " +
            $"감사체인 {(result.AuditChainValid ? "정상" : "오류")} · " +
            $"항목 {result.CheckedEntries}개 · 실패 {result.FailedEntries}개 · 복구 {result.RepairedEntries}건";

        var dialog = new ContentDialog
        {
            Title = result.Success ? "무결성 검사 완료" : "무결성 검사 — 조치 필요",
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "확인",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
        RefreshEntryList();
    }

    private void ClearPasswordBoxes()
    {
        CreatePasswordBox.Password = "";
        CreatePasswordConfirmBox.Password = "";
        RecoveryHintBox.Text = "";
    }

    private void SetUnlockedActionsEnabled(bool enabled)
    {
        SetControlsEnabled(UnlockedActionsPanel, enabled);
        EntryList.IsEnabled = enabled;
    }

    private static void SetControlsEnabled(DependencyObject parent, bool enabled)
    {
        var childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Control control)
            {
                control.IsEnabled = enabled;
            }

            SetControlsEnabled(child, enabled);
        }
    }

    private async Task<SecureVaultOperationResult> RunVaultOperationWithProgressAsync(
        string dialogTitle,
        Func<IProgress<SecureVaultProgressReport>, Task<SecureVaultOperationResult>> operation)
    {
        _operationInProgress = true;
        _autoLockTimer.Stop();
        SetUnlockedActionsEnabled(false);

        var progressDialog = new SecureVaultOperationProgressDialog(dialogTitle, XamlRoot);
        var progress = progressDialog.CreateProgress();
        progressDialog.Show();

        try
        {
            return await operation(progress);
        }
        catch (OperationCanceledException)
        {
            return new SecureVaultOperationResult { Success = false, Message = "금고 작업을 취소했습니다." };
        }
        catch (Exception ex)
        {
            Services.CrashCaptureService.WriteCrash(
                "vault-operation-local",
                null,
                $"Type: {ex.GetType().FullName} · HRESULT: 0x{ex.HResult:X8}");
            return new SecureVaultOperationResult
            {
                Success = false,
                Message = $"금고 작업 중 오류가 발생했습니다. 오류 코드: 0x{ex.HResult:X8}"
            };
        }
        finally
        {
            progressDialog.Hide();
            SetUnlockedActionsEnabled(true);
            _operationInProgress = false;
            ResetAutoLockTimer();
        }
    }
}