using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ErrorBundlePage : Page
{
    private readonly ErrorBundleService _errorBundleService = new();

    public ErrorBundlePage()
    {
        InitializeComponent();
        Refresh();
    }

    private void RefreshSources(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void CreateErrorBundle(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _errorBundleService.CreateBundle();
            StatusText.Text = $"오류 번들 생성 완료: {path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"오류 번들 생성 실패: {ex.Message}";
        }
    }

    private void OpenBundleFolder(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_errorBundleService.BundleRoot);
        Process.Start(new ProcessStartInfo
        {
            FileName = _errorBundleService.BundleRoot,
            UseShellExecute = true
        });
    }

    private void Refresh()
    {
        SourceList.ItemsSource = _errorBundleService.InspectSources();
    }
}
