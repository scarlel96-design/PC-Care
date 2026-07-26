using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class KnowledgePackManagerPage : Page
{
    private readonly KnowledgePackVerifier _verifier = new();

    public KnowledgePackManagerPage()
    {
        InitializeComponent();
        Verify();
    }

    private void Verify(object sender, RoutedEventArgs e) => Verify();

    private void Verify()
    {
        var result = _verifier.Verify();
        StatusText.Text = result.Message;
        RulesChecksumText.Text = string.IsNullOrWhiteSpace(result.RulesChecksum)
            ? result.RulesStatus
            : $"{result.RulesStatus} · SHA256: {result.RulesChecksum}";
        ProtocolsChecksumText.Text = string.IsNullOrWhiteSpace(result.ProtocolsChecksum)
            ? result.ProtocolsStatus
            : $"{result.ProtocolsStatus} · SHA256: {result.ProtocolsChecksum}";
    }
}