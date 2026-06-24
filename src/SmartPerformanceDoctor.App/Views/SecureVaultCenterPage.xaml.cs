using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Input;
using SmartPerformanceDoctor.App.Models.Security;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SecureVaultCenterPage : Page
{
    private readonly SecureVaultViewModel _viewModel = new();
    private readonly DispatcherTimer _autoLockTimer;
    private bool _operationInProgress;

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
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.AutoLockRequested += OnAutoLockActivity;
        Loaded += OnPageLoaded;
        Unloaded += OnUnloaded;
        InitializeAutoLockMinutesBox();
        SyncBoundText();
        UpdatePanelVisibility();
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
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.AutoLockRequested -= OnAutoLockActivity;
    }

    private void OnAutoLockTick(object? sender, object e)
    {
        if (!_viewModel.IsUnlocked)
        {
            _autoLockTimer.Stop();
            return;
        }

        _viewModel.Lock();
        _viewModel.SetStatus($"비활성 {_viewModel.AutoLockMinutes}분 경과 — 금고를 자동 잠갔습니다.");
        _autoLockTimer.Stop();
    }

    private void OnAutoLockActivity(object? sender, EventArgs e) => ResetAutoLockTimer();

    private void ResetAutoLockTimer()
    {
        _autoLockTimer.Stop();
        if (_viewModel.IsUnlocked)
        {
            _autoLockTimer.Interval = TimeSpan.FromMinutes(_viewModel.AutoLockMinutes);
            _autoLockTimer.Start();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SecureVaultViewModel.StateLine)
            or nameof(SecureVaultViewModel.StatusLine)
            or nameof(SecureVaultViewModel.CryptoLine)
            or nameof(SecureVaultViewModel.SecurityLine)
            or nameof(SecureVaultViewModel.Breadcrumb)
            or nameof(SecureVaultViewModel.VisibleItems)
            or null
            or "")
        {
            SyncBoundText();
        }

        if (e.PropertyName is nameof(SecureVaultViewModel.NeedsKdfMigration))
        {
            KdfMigrationPanel.Visibility = _viewModel.NeedsKdfMigration && _viewModel.IsUnlocked
                ? Visibility.Visible
                : Visibility.Collapsed;
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
        KdfMigrationPanel.Visibility = _viewModel.NeedsKdfMigration && _viewModel.IsUnlocked
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SyncBoundText()
    {
        StateLineText.Text = _viewModel.StateLine;
        StatusLineText.Text = _viewModel.StatusLine;
        CryptoLineText.Text = _viewModel.CryptoLine;
        SecurityLineText.Text = _viewModel.SecurityLine;
        BreadcrumbText.Text = _viewModel.Breadcrumb;
        RefreshEntryList();
    }

    private void RefreshEntryList()
    {
        EntryList.ItemsSource = null;
        EntryList.ItemsSource = _viewModel.VisibleItems;
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
        if (result.Success && !string.IsNullOrWhiteSpace(result.RecoveryKey))
        {
            _ = ShowRecoveryKeyDialogAsync("금고 생성 완료", result.RecoveryKey);
        }

        ClearPasswordBoxes();
    }

    private void UnlockWithRecoveryKey(object sender, RoutedEventArgs e)
    {
        _viewModel.UnlockWithRecoveryKey(RecoveryKeyBox.Text);
        RecoveryKeyBox.Text = "";
    }

    private async void MigrateKdf(object sender, RoutedEventArgs e)
    {
        var passwordBox = new PasswordBox { PlaceholderText = "마스터 비밀번호" };
        var dialog = new ContentDialog
        {
            Title = "Argon2id KDF 업그레이드",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "PBKDF2에서 Argon2id로 키 유도 방식을 업그레이드합니다. 새 복구 키가 발급되니 안전한 곳에 보관하세요.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    passwordBox
                }
            },
            PrimaryButtonText = "업그레이드",
            CloseButtonText = "취소",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = _viewModel.MigrateKdfToArgon2(passwordBox.Password);
        if (result.Success && !string.IsNullOrWhiteSpace(result.RecoveryKey))
        {
            await ShowRecoveryKeyDialogAsync("KDF 업그레이드 완료", result.RecoveryKey);
        }
        else if (!result.Success)
        {
            await ShowOperationResultDialogAsync("KDF 업그레이드 실패", result);
        }
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
        if (result.Success && !string.IsNullOrWhiteSpace(result.RecoveryKey))
        {
            await ShowRecoveryKeyDialogAsync("비밀번호 변경 완료", result.RecoveryKey);
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
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = $"아래 복구 키를 안전한 곳에 보관하세요. 분실 시 비밀번호 없이 금고를 열 수 없습니다.\n\n{recoveryKey}",
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            },
            PrimaryButtonText = "복사 완료",
            CloseButtonText = "닫기",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void UnlockVault(object sender, RoutedEventArgs e)
    {
        _viewModel.Unlock(UnlockPasswordBox.Password);
        UnlockPasswordBox.Password = "";
    }

    private void LockVault(object sender, RoutedEventArgs e) => _viewModel.Lock();

    private async void AddFile(object sender, RoutedEventArgs e)
    {
        if (_operationInProgress)
        {
            return;
        }

        var path = await SecureVaultPickerService.PickFileAsync(App.Shell);
        if (path is null)
        {
            return;
        }

        var result = await RunVaultOperationWithProgressAsync(
            "파일 잠금 · 보관",
            progress => _viewModel.AddFileAsync(path, progress));
        await ShowOperationResultDialogAsync(result.Success ? "보관 완료" : "보관 실패", result);
        RefreshEntryList();
    }

    private async void AddFolder(object sender, RoutedEventArgs e)
    {
        if (_operationInProgress)
        {
            return;
        }

        var path = await SecureVaultPickerService.PickFolderAsync(App.Shell);
        if (path is null)
        {
            return;
        }

        var result = await RunVaultOperationWithProgressAsync(
            "폴더 잠금 · 보관",
            progress => _viewModel.AddFolderAsync(path, progress));
        await ShowOperationResultDialogAsync(result.Success ? "보관 완료" : "보관 실패", result);
        RefreshEntryList();
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

    private async void ExportEntry(object sender, RoutedEventArgs e)
    {
        if (GetSelectedItem() is not { } item)
        {
            _viewModel.SetStatus("보낼 항목을 목록에서 선택하세요.");
            return;
        }

        var destination = await SecureVaultPickerService.PickExportFolderAsync(App.Shell);
        if (destination is null)
        {
            return;
        }

        await _viewModel.ExportEntryAsync(item, destination);
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
        finally
        {
            progressDialog.Hide();
            SetUnlockedActionsEnabled(true);
            _operationInProgress = false;
            ResetAutoLockTimer();
        }
    }
}