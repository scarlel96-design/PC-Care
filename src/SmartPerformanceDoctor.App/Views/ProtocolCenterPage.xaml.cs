using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ProtocolCenterPage : Page
{
    private readonly ProtocolCenterService _service = new();
    private IReadOnlyList<ProtocolDetail> _protocols = Array.Empty<ProtocolDetail>();

    public ProtocolCenterPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        _protocols = _service.LoadAll();
        SummaryText.Text = $"복구 방법 {_protocols.Count}개 · 미리보기 후 승인하고 적용합니다";
        ProtocolList.ItemsSource = _protocols.Select(p => $"{p.ProtocolId} · {p.Area} · 위험:{p.Risk}").ToArray();
        DryRunText.Text = "프로토콜을 선택하세요.";
    }

    private void OnProtocolSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ProtocolList.SelectedIndex < 0 || ProtocolList.SelectedIndex >= _protocols.Count)
        {
            return;
        }

        DryRunText.Text = _service.BuildDryRunPreview(_protocols[ProtocolList.SelectedIndex]);
    }
}