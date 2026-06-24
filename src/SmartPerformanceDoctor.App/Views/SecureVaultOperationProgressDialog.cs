using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models.Security;

namespace SmartPerformanceDoctor.App.Views;

internal sealed class SecureVaultOperationProgressDialog
{
    private readonly ContentDialog _dialog;
    private readonly TextBlock _phaseText;
    private readonly TextBlock _detailText;
    private readonly TextBlock _itemText;
    private readonly TextBlock _countText;
    private readonly ProgressBar _progressBar;

    public SecureVaultOperationProgressDialog(string title, XamlRoot xamlRoot)
    {
        _phaseText = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        _detailText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Application.Current.Resources["SpdMutedTextBrush"] as Microsoft.UI.Xaml.Media.Brush
        };
        _itemText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 48,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _countText = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = Application.Current.Resources["SpdMutedTextBrush"] as Microsoft.UI.Xaml.Media.Brush
        };
        _progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 8
        };

        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        panel.Children.Add(_phaseText);
        panel.Children.Add(_detailText);
        panel.Children.Add(_progressBar);
        panel.Children.Add(_itemText);
        panel.Children.Add(_countText);

        _dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            XamlRoot = xamlRoot
        };

        ApplyReport(new SecureVaultProgressReport
        {
            Phase = SecureVaultProgressPhase.Preparing,
            Percent = 0,
            Title = "준비 중",
            Detail = "작업을 시작합니다."
        });
    }

    public IProgress<SecureVaultProgressReport> CreateProgress() =>
        new Progress<SecureVaultProgressReport>(ApplyReport);

    public void Show() => _ = _dialog.ShowAsync();

    public void Hide() => _dialog.Hide();

    private void ApplyReport(SecureVaultProgressReport report)
    {
        _phaseText.Text = FormatPhaseLine(report);
        _detailText.Text = report.Detail;
        _progressBar.Value = report.Percent;
        _progressBar.IsIndeterminate = report.Phase == SecureVaultProgressPhase.Preparing && report.Percent <= 0;

        _itemText.Text = string.IsNullOrWhiteSpace(report.CurrentItem)
            ? ""
            : report.CurrentItem;
        _itemText.Visibility = string.IsNullOrWhiteSpace(report.CurrentItem)
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (report.TotalCount > 0)
        {
            _countText.Text = $"{report.ProcessedCount}/{report.TotalCount}";
            _countText.Visibility = Visibility.Visible;
        }
        else
        {
            _countText.Text = "";
            _countText.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatPhaseLine(SecureVaultProgressReport report)
    {
        var prefix = report.Phase switch
        {
            SecureVaultProgressPhase.Unsealing => "🔓 ",
            SecureVaultProgressPhase.Sealing => "🔒 ",
            SecureVaultProgressPhase.Restoring => "📂 ",
            SecureVaultProgressPhase.Adding => "📥 ",
            SecureVaultProgressPhase.RemovingFromVault => "🧹 ",
            SecureVaultProgressPhase.Completed => "✓ ",
            _ => ""
        };

        return prefix + report.Title;
    }
}