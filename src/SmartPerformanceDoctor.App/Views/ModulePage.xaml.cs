using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ModulePage : Page
{
    private readonly ModuleViewModel _viewModel = new();
    private ModuleDescriptor _module = ModuleRegistry.Get("system");
    private bool _isRunning;
    private bool _autoRunPending;

    public ModulePage()
    {
        InitializeComponent();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        var request = e.Parameter switch
        {
            ModuleNavigationRequest typed => typed,
            string moduleId => new ModuleNavigationRequest(moduleId),
            _ => new ModuleNavigationRequest("system")
        };

        ApplyNavigation(request);
    }

    public void ApplyNavigation(ModuleNavigationRequest request)
    {
        _module = ModuleRegistry.Get(request.ModuleId);
        _autoRunPending = request.AutoRun;

        TitleText.Text = _module.Title;
        SubtitleText.Text = _module.Subtitle;
        PipelineList.ItemsSource = _module.Pipeline;
        _viewModel.ResetForModule(_module);
        SyncUiFromViewModel();
        IntelligenceText.Text = "아직 결과가 없습니다.";

        if (_autoRunPending)
        {
            _autoRunPending = false;
            _ = RunModuleCoreAsync();
        }
    }

    private async void RunModule(object sender, RoutedEventArgs e)
    {
        await RunModuleCoreAsync();
    }

    private async Task RunModuleCoreAsync()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        RunButton.IsEnabled = false;

        try
        {
            IntelligenceText.Text = "엔진이 진단을 수행하는 중입니다...";
            await _viewModel.RunAsync(_module, CancellationToken.None);
            IntelligenceText.Text = FormatIntelligence();
            SyncUiFromViewModel();
        }
        catch (Exception ex)
        {
            _viewModel.SetFailure($"진단 실행 중 오류: {ex.Message}");
            IntelligenceText.Text = ex.Message;
            SyncUiFromViewModel();
        }
        finally
        {
            _isRunning = false;
            RunButton.IsEnabled = true;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(SyncUiFromViewModel);
    }

    private void SyncUiFromViewModel()
    {
        Progress.Value = _viewModel.Progress;
        StatusText.Text = _viewModel.Status;
        LatestMessageText.Text = _viewModel.LatestMessage;
        EventList.ItemsSource = _viewModel.Events
            .Select(evt => $"{evt.Progress,3}%  {evt.Message}  ({evt.Severity})")
            .ToList();
    }

    private string FormatIntelligence()
    {
        if (_viewModel.Intelligence is not null)
        {
            return
                $"{_viewModel.Intelligence.Status} / {_viewModel.Intelligence.Score}점\n" +
                $"{_viewModel.Intelligence.PlainSummary}\n\n" +
                FormatReportPaths();
        }

        if (string.Equals(_viewModel.Status, "core-not-found", StringComparison.OrdinalIgnoreCase))
        {
            return "Core 실행 파일이 없습니다. publish/copy-native-engines 단계를 확인하세요.";
        }

        if (string.Equals(_viewModel.Status, "no-final-response", StringComparison.OrdinalIgnoreCase))
        {
            return $"{_viewModel.LatestMessage}\n\nCore 프로세스는 시작됐지만 최종 응답을 받지 못했습니다.";
        }

        return string.IsNullOrWhiteSpace(_viewModel.LatestMessage)
            ? "결과를 표시할 수 없습니다."
            : _viewModel.LatestMessage;
    }

    private string FormatReportPaths()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(_viewModel.HtmlReportPath))
        {
            lines.Add($"HTML: {_viewModel.HtmlReportPath}");
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.JsonReportPath))
        {
            lines.Add($"JSON: {_viewModel.JsonReportPath}");
        }

        return lines.Count == 0 ? string.Empty : string.Join('\n', lines);
    }
}