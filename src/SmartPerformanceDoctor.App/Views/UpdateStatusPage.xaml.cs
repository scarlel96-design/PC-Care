using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Update;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class UpdateStatusPage : Page
{
    private readonly UpdateStatusViewModel _viewModel = new();

    public UpdateStatusPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.Refresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UiDispatcher.Queue ??= DispatcherQueue.GetForCurrentThread();
    }

    private void RefreshStatus(object sender, RoutedEventArgs e)
    {
        _viewModel.Refresh();
    }

    private async void PickUpdateFile(object sender, RoutedEventArgs e)
    {
        var path = await UpdateFilePickerService.PickUpdatePackageAsync(App.Shell);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _viewModel.SetSelectedPackageAsync(path);
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