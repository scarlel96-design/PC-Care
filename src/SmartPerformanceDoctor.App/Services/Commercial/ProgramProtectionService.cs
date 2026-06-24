using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Branding;

namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed class ProgramProtectionReport
{
    public bool Passed { get; init; }
    public string ExePath { get; init; } = "";
    public string ExeSha256 { get; init; } = "";
    public string Message { get; init; } = "";
    public IReadOnlyList<string> Findings { get; init; } = Array.Empty<string>();
}

public sealed class ProgramProtectionService
{
    public ProgramProtectionReport VerifyInstallIntegrity()
    {
        var baseDir = AppContext.BaseDirectory;
        var exePath = SmartPerformanceDoctor.Aegis.AppExecutableResolver.ResolveMainExecutable(baseDir)
            ?? Path.Combine(baseDir, "SmartPerformanceDoctor.exe");
        var dllPath = Path.Combine(baseDir, "SmartPerformanceDoctor.dll");
        var findings = new List<string>();

        if (!File.Exists(exePath))
        {
            findings.Add("PCCare.exe 누락");
        }

        if (!File.Exists(dllPath))
        {
            findings.Add("SmartPerformanceDoctor.dll 누락");
        }

        var corePath = RuntimePaths.ResolveCoreEnginePath();
        if (!File.Exists(corePath))
        {
            findings.Add($"{AstraCareBranding.Engine} 엔진 미탑재 (일부 점검 제한)");
        }

        var pack = new KnowledgePackVerifier().Verify();
        if (!pack.RulesValid || !pack.ProtocolsValid)
        {
            findings.Add($"Knowledge Pack 검증: {pack.Message}");
        }

        var exeHash = File.Exists(exePath)
            ? ComputeSha256(exePath)
            : "";

        return new ProgramProtectionReport
        {
            Passed = findings.Count == 0,
            ExePath = exePath,
            ExeSha256 = exeHash,
            Message = findings.Count == 0 ? "프로그램 무결성 검사 통과" : "무결성 경고 발견",
            Findings = findings
        };
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}