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
        TrustSummaryText.Text = CommercialPackTrustState.IsFullyTrusted
            ? "Pack trust: checksum·ECDSA 서명 검증 통과"
            : $"Pack trust: 제한 모드 — {CommercialPackTrustState.Message}";
        RulesChecksumText.Text = $"rules.pack.json SHA256: {result.RulesChecksum} ({(result.RulesValid ? "OK" : "FAIL")})";
        RulesSignatureText.Text = $"rules.pack.json 서명: {(result.RulesSignatureValid ? "정상 (ECDSA)" : "오류 또는 없음")}";
        ProtocolsChecksumText.Text = $"protocols.pack.json SHA256: {result.ProtocolsChecksum} ({(result.ProtocolsValid ? "OK" : "FAIL")})";
        ProtocolsSignatureText.Text = $"protocols.pack.json 서명: {(result.ProtocolsSignatureValid ? "정상 (ECDSA)" : "오류 또는 없음")}";
    }
}