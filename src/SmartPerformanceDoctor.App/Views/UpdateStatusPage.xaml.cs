using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Update;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class UpdateStatusPage : Page
{
    private static bool _triggerGitHubCheckOnLoad;

    private readonly UpdateStatusViewModel _viewModel = new();

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
    }

    private async void PickUpdateFile(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
        }

        try
        {
            var path = await UpdateFilePickerService.PickUpdatePackageAsync(App.Shell);
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            await _viewModel.SetSelectedPackageAsync(path);
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("update-pick-file", ex, ex.Message);
            _viewModel.Refresh();
        }
        finally
        {
            if (button is not null)
            {
                button.IsEnabled = true;
            }
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