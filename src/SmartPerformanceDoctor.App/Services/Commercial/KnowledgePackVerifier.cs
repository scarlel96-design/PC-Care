namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class KnowledgePackVerification
{
    public bool RulesValid { get; init; }
    public bool ProtocolsValid { get; init; }
    public bool RulesSignatureValid { get; init; }
    public bool ProtocolsSignatureValid { get; init; }
    public string RulesChecksum { get; init; } = "";
    public string ProtocolsChecksum { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class KnowledgePackVerifier
{
    public KnowledgePackVerification Verify()
    {
        var root = RuntimePaths.CommercialDataDirectory;
        var rules = CommercialPackTrust.VerifyPack(root, "rules.pack.json");
        var protocols = CommercialPackTrust.VerifyPack(root, "protocols.pack.json");

        if (!File.Exists(Path.Combine(root, "rules.pack.json")) || !File.Exists(Path.Combine(root, "protocols.pack.json")))
        {
            return new KnowledgePackVerification
            {
                Message = "Knowledge Pack 파일을 찾을 수 없습니다."
            };
        }

        var ok = rules.IsTrusted && protocols.IsTrusted;
        return new KnowledgePackVerification
        {
            RulesValid = rules.ChecksumValid,
            ProtocolsValid = protocols.ChecksumValid,
            RulesSignatureValid = rules.SignatureValid,
            ProtocolsSignatureValid = protocols.SignatureValid,
            RulesChecksum = rules.Checksum,
            ProtocolsChecksum = protocols.Checksum,
            Message = ok
                ? "Rule/Protocol Pack checksum·서명 검증 통과"
                : $"Pack trust 실패 — rules:{rules.Message}, protocols:{protocols.Message}"
        };
    }
}