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
        RulesChecksumText.Text = $"rules.pack.json SHA256: {result.RulesChecksum} ({(result.RulesValid ? "OK" : "FAIL")})";
        ProtocolsChecksumText.Text = $"protocols.pack.json SHA256: {result.ProtocolsChecksum} ({(result.ProtocolsValid ? "OK" : "FAIL")})";
    }
}