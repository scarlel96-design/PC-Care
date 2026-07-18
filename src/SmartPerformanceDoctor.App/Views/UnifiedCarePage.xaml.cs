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

    public UnifiedCarePage()
    {
        InitializeComponent();
        ApplyDefaultMode();
        _viewModel.PropertyChanged += OnViewModelChanged;
        _viewModel.Steps.CollectionChanged += OnStepsChanged;
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

    private void SyncUi()
    {
        ProgressBar.Value = _viewModel.Progress;
        StatusText.Text = _viewModel.Status;
        SummaryText.Text = _viewModel.Summary;
        SessionText.Text = _viewModel.SessionLine;
        StepList.ItemsSource = null;
        StepList.ItemsSource = _viewModel.Steps;
        StartButton.IsEnabled = !_viewModel.IsRunning;
    }
}