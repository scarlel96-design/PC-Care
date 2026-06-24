using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SystemCareCenterPage : Page
{
    private readonly SystemCareViewModel _viewModel = new();

    public SystemCareCenterPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        SmartScanRadio.IsChecked = true;
        UpdateChecklistVisibility();
    }

    private void ScanModeChanged(object sender, RoutedEventArgs e)
    {
        if (PrecisionScanRadio.IsChecked == true)
        {
            _viewModel.SelectedModeIndex = 1;
        }
        else if (SmartScanRadio.IsChecked == true)
        {
            _viewModel.SelectedModeIndex = 0;
        }

        UpdateChecklistVisibility();
    }

    private void UpdateChecklistVisibility()
    {
        TaskChecklistPanel.Visibility = _viewModel.IsPrecisionMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectAllTasks(object sender, RoutedEventArgs e) => _viewModel.SelectAllTasks();

    private void ClearAllTasks(object sender, RoutedEventArgs e) => _viewModel.ClearAllTasks();

    private async void StartScan(object sender, RoutedEventArgs e) =>
        await _viewModel.StartScanAsync();

    private async void PreviewChanges(object sender, RoutedEventArgs e) =>
        await _viewModel.PreviewChangesAsync();

    private async void ApplyChanges(object sender, RoutedEventArgs e) =>
        await _viewModel.ApplySafeAsync(IncludeReviewBox.IsChecked == true);

    private async void RollbackChanges(object sender, RoutedEventArgs e) =>
        await _viewModel.RollbackAsync();
}