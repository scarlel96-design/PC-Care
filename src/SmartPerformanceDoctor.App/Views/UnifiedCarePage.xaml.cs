using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class UnifiedCarePage : Page
{
    private readonly UnifiedCareViewModel _viewModel = new();
    private CancellationTokenSource? _runCts;
    private Task? _runTask;
    private bool _isLoaded;
    private bool _pendingAutoStart;
    private readonly DispatcherTimer _progressAnimationTimer = new();
    private double _displayedProgress;
    private double _targetProgress;

    public UnifiedCarePage()
    {
        InitializeComponent();
        ApplyDefaultMode();
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Steps.CollectionChanged += OnStepsChanged;
        _progressAnimationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _progressAnimationTimer.Tick += AnimateProgress;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ApplyNavigationParameter(e.Parameter);
    }

    public void ApplyNavigation(CareNavigationRequest request) => ApplyNavigationParameter(request);

    private void ApplyNavigationParameter(object? parameter)
    {
        switch (parameter)
        {
            case CareNavigationRequest request:
                ApplyRequest(request);
                break;
            case string scope:
                ApplyRequest(new CareNavigationRequest(scope));
                break;
        }
    }

    private void ApplyDefaultMode()
    {
        DiagnosisOnlyRadio.IsChecked = true;
        DiagnosisAndRepairRadio.IsChecked = false;
        _viewModel.IncludeRepair = false;
        _viewModel.RiskAccepted = false;
        RiskCheck.IsChecked = false;
        RiskCheck.IsEnabled = false;
    }

    private void ApplyRequest(CareNavigationRequest request)
    {
        CancelRunning();
        _pendingAutoStart = false;

        _viewModel.ResetSessionCore();
        _viewModel.ApplyScope(request.Scope);
        SelectScope(request.Scope);

        DiagnosisOnlyRadio.IsChecked = !request.IncludeRepair;
        DiagnosisAndRepairRadio.IsChecked = request.IncludeRepair;

        ReadOptionsFromUi();
        SyncUi();

        if (request.AutoStart)
        {
            if (_isLoaded)
            {
                _ = RunCareAsync();
            }
            else
            {
                _pendingAutoStart = true;
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UiDispatcher.Queue ??= DispatcherQueue.GetForCurrentThread();

        if (_pendingAutoStart)
        {
            _pendingAutoStart = false;
            _ = RunCareAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _progressAnimationTimer.Stop();
        CancelRunning();
    }

    private void CancelRunning()
    {
        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = null;
    }

    private void SelectScope(string scope)
    {
        foreach (var item in ScopeBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, scope, StringComparison.OrdinalIgnoreCase))
            {
                ScopeBox.SelectedItem = item;
                return;
            }
        }
    }

    private async void StartCare(object sender, RoutedEventArgs e) => await RunCareAsync();

    private void CancelCare(object sender, RoutedEventArgs e)
    {
        CancelRunning();
        StatusText.Text = "중단 요청 중…";
        SummaryText.Text = "현재 처리 단계가 안전하게 끝난 뒤 점검을 중단합니다.";
    }

    private async Task RunCareAsync()
    {
        CancelRunning();
        _runTask = null;

        ReadOptionsFromUi();
        _viewModel.ResetSessionCore();
        SyncUi();

        _runCts = new CancellationTokenSource();

        StartButton.IsEnabled = false;
        StatusText.Text = "준비 중…";
        ProgressBar.Value = 4;
        SummaryText.Text = "점검 엔진을 시작합니다…";

        try
        {
            _runTask = _viewModel.RunAsync(_runCts.Token);
            await _runTask.ConfigureAwait(true);
        }
        finally
        {
            StartButton.IsEnabled = !_viewModel.IsRunning;
            SyncUi();
        }
    }

    private void ScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScopeBox.SelectedItem is ComboBoxItem item && item.Tag is string scope)
        {
            _viewModel.Scope = scope;
        }
    }

    private void ModeChanged(object sender, RoutedEventArgs e) => ReadOptionsFromUi();

    private void RiskChanged(object sender, RoutedEventArgs e) => _viewModel.RiskAccepted = RiskCheck.IsChecked == true;

    private void ReadOptionsFromUi()
    {
        if (ScopeBox.SelectedItem is ComboBoxItem item && item.Tag is string scope)
        {
            _viewModel.Scope = scope;
        }

        _viewModel.IncludeRepair = DiagnosisAndRepairRadio.IsChecked == true;
        RiskCheck.IsEnabled = _viewModel.IncludeRepair;
        if (!_viewModel.IncludeRepair)
        {
            RiskCheck.IsChecked = false;
            _viewModel.RiskAccepted = false;
        }

        ModeHintText.Text = _viewModel.IncludeRepair
            ? "현재: 진단+복구 모드입니다. 문제가 발견되면 사전 확인 후 실제 복구가 진행됩니다."
            : "현재: 진단만 모드입니다. 복구 작업은 실행되지 않습니다.";
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, SyncUi);

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, SyncUi);

    private void SetProgressTarget(double value)
    {
        var next = Math.Clamp(value, 0, 100);
        if (next <= 0 || next < _displayedProgress)
        {
            _displayedProgress = next;
            _targetProgress = next;
            ApplyDisplayedProgress();
            return;
        }

        _targetProgress = Math.Max(_targetProgress, next);
        if (Math.Abs(_targetProgress - _displayedProgress) > 0.05)
        {
            _progressAnimationTimer.Start();
        }
    }

    private void AnimateProgress(object? sender, object e)
    {
        var remaining = _targetProgress - _displayedProgress;
        if (remaining <= 0.08)
        {
            _displayedProgress = _targetProgress;
            _progressAnimationTimer.Stop();
            ApplyDisplayedProgress();
            return;
        }

        _displayedProgress += Math.Max(0.18, remaining * 0.16);
        _displayedProgress = Math.Min(_displayedProgress, _targetProgress);
        ApplyDisplayedProgress();
    }

    private void ApplyDisplayedProgress()
    {
        ProgressBar.Value = _displayedProgress;
        ProgressPercentText.Text = $"{Math.Clamp((int)Math.Round(_displayedProgress), 0, 100)}%";
    }
    private void SyncUi()
    {
        SetProgressTarget(_viewModel.Progress);
        StatusText.Text = _viewModel.Status;
        SummaryText.Text = _viewModel.Summary;
        SessionText.Text = _viewModel.SessionLine;
        ProgressFlowText.Text = _viewModel.ProgressFlow;
        StepList.ItemsSource = null;
        StepList.ItemsSource = _viewModel.Steps;
        StartButton.IsEnabled = !_viewModel.IsRunning;
        ScopeBox.IsEnabled = !_viewModel.IsRunning;
        DiagnosisOnlyRadio.IsEnabled = !_viewModel.IsRunning;
        DiagnosisAndRepairRadio.IsEnabled = !_viewModel.IsRunning;
        RiskCheck.IsEnabled = !_viewModel.IsRunning && _viewModel.IncludeRepair;
        CancelButton.Visibility = _viewModel.IsRunning ? Visibility.Visible : Visibility.Collapsed;
    }
}
