namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class KnowledgePackVerification
{
    public bool RulesValid { get; init; }
    public bool ProtocolsValid { get; init; }
    public bool RulesSignatureValid { get; init; }
    public bool ProtocolsSignatureValid { get; init; }
    public string RulesChecksum { get; init; } = "";
    public string ProtocolsChecksum { get; init; } = "";
    public string RulesStatus { get; init; } = "";
    public string ProtocolsStatus { get; init; } = "";
    public string Message { get; init; } = "";
}

public sealed class KnowledgePackVerifier
{
    public KnowledgePackVerification Verify()
    {
        var root = RuntimePaths.CommercialDataDirectory;
        var rules = CommercialPackTrust.VerifyPack(root, "rules.pack.json");
        var protocols = CommercialPackTrust.VerifyPack(root, "protocols.pack.json");
        var hasRulesPack = File.Exists(Path.Combine(root, "rules.pack.json"));
        var hasProtocolsPack = File.Exists(Path.Combine(root, "protocols.pack.json"));
        var builtInRulesAvailable = Directory.Exists(RuntimePaths.RulesDirectory);

        if (!hasRulesPack || !hasProtocolsPack)
        {
            return new KnowledgePackVerification
            {
                Message = builtInRulesAvailable
                    ? "내장 규칙 데이터 사용 중 · 선택 Pack 파일은 설치되지 않았습니다."
                    : "규칙 데이터 파일을 찾을 수 없습니다.",
                RulesStatus = hasRulesPack ? "rules.pack.json 파일 있음" : "rules.pack.json 없음",
                ProtocolsStatus = hasProtocolsPack ? "protocols.pack.json 파일 있음" : "protocols.pack.json 없음"
            };
        }

        if (rules.IsTrusted && protocols.IsTrusted)
        {
            return new KnowledgePackVerification
            {
                RulesValid = true,
                ProtocolsValid = true,
                RulesSignatureValid = true,
                ProtocolsSignatureValid = true,
                RulesChecksum = rules.Checksum,
                ProtocolsChecksum = protocols.Checksum,
                RulesStatus = "rules.pack.json · checksum·서명 검증 통과",
                ProtocolsStatus = "protocols.pack.json · checksum·서명 검증 통과",
                Message = "규칙 데이터 검증 통과"
            };
        }

        // Personal/offline builds may intentionally omit a release signing key.  The product
        // continues with the bundled local rule catalogue, while exposing that this is a
        // local-build fallback rather than falsely claiming a signed Pack is trusted.
        if (builtInRulesAvailable)
        {
            return new KnowledgePackVerification
            {
                RulesValid = rules.ChecksumValid,
                ProtocolsValid = protocols.ChecksumValid,
                RulesSignatureValid = rules.SignatureValid,
                ProtocolsSignatureValid = protocols.SignatureValid,
                RulesChecksum = rules.Checksum,
                ProtocolsChecksum = protocols.Checksum,
                RulesStatus = $"rules.pack.json · {rules.Message} · 내장 규칙 데이터 사용",
                ProtocolsStatus = $"protocols.pack.json · {protocols.Message} · 내장 규칙 데이터 사용",
                Message = "내장 규칙 데이터 정상 사용 중 · 개인 빌드에서는 외부 Pack 서명을 요구하지 않습니다."
            };
        }

        return new KnowledgePackVerification
        {
            RulesValid = rules.ChecksumValid,
            ProtocolsValid = protocols.ChecksumValid,
            RulesSignatureValid = rules.SignatureValid,
            ProtocolsSignatureValid = protocols.SignatureValid,
            RulesChecksum = rules.Checksum,
            ProtocolsChecksum = protocols.Checksum,
            RulesStatus = $"rules.pack.json · {rules.Message}",
            ProtocolsStatus = $"protocols.pack.json · {protocols.Message}",
            Message = $"규칙 데이터 검증 실패 — rules:{rules.Message}, protocols:{protocols.Message}"
        };
    }
}