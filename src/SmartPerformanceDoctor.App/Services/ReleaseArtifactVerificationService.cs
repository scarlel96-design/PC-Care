using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class ReleaseArtifactVerificationService
{
    public const string Version = "44.0.0";

    public ReleaseGateResult Evaluate()
    {
        var baseDir = RuntimePaths.InstallRoot;
        var distDir = FindDistRoot();

        var candidates = new[]
        {
            ("메인 프로그램", "실행", Path.Combine(baseDir, "SmartPerformanceDoctor.exe")),
            ("진단 엔진", "실행", RuntimePaths.ResolveEnginePath("smart_performance_doctor_core.exe")),
            ("복구 도우미", "실행", RuntimePaths.ResolveEnginePath("smart_performance_doctor_repair_helper.exe")),
            ("포터블 ZIP", "포터블", Path.Combine(distDir, "SmartPerformanceDoctor_v44_Portable.zip")),
            ("설치 레이아웃", "설치", Path.Combine(distDir, "installer", "layout")),
            ("WiX MSI", "설치", FindInstallerArtifact("*.msi")),
            ("MSIX 패키지", "설치", FindInstallerArtifact("*.msix"))
        };

        var artifacts = candidates.Select(x => BuildArtifact(x.Item1, x.Item2, x.Item3)).ToArray();

        var gates = new List<ReleaseGateItem>
        {
            CheckPath("메인 프로그램", "실행", Path.Combine(baseDir, "SmartPerformanceDoctor.exe"), "높음"),
            CheckPath("진단 엔진", "실행", RuntimePaths.ResolveEnginePath("smart_performance_doctor_core.exe"), "높음"),
            CheckPath("복구 도우미", "실행", RuntimePaths.ResolveEnginePath("smart_performance_doctor_repair_helper.exe"), "높음"),
            CheckPath("진단 규칙", "구성", RuntimePaths.RulesDirectory, "보통"),
            CheckPath("디자인 리소스", "구성", RuntimePaths.AssetsDirectory, "보통"),
            CheckPath("포터블 ZIP", "포터블", Path.Combine(distDir, "SmartPerformanceDoctor_v44_Portable.zip"), "보통"),
            CheckPath("WiX 설치 스크립트", "설치", FindProjectFile("scripts\\build-wix-msi.ps1"), "보통"),
            CheckPath("MSIX 빌드 스크립트", "설치", FindProjectFile("scripts\\build-msix.ps1"), "보통"),
            CheckPath("서명 스크립트", "서명", FindProjectFile("scripts\\sign-consumer.ps1"), "보통")
        };

        gates.Add(CheckDesktopFolder("보고서 폴더", "지원", RuntimePaths.ReportsRoot, "낮음"));
        gates.Add(CheckDesktopFolder("복구 기록 폴더", "지원", RuntimePaths.RepairLogsRoot, "낮음"));
        gates.Add(CheckDesktopFolder("오류 기록 폴더", "지원", RuntimePaths.CrashLogsRoot, "낮음"));
        gates.Add(CheckDesktopFolder("문제 보고서 폴더", "지원", RuntimePaths.ErrorBundlesRoot, "낮음"));

        var failed = gates.Count(x => x.Status == "실패");
        var warnings = gates.Count(x => x.Status == "주의");
        var status = failed > 0 ? "미준비" : warnings > 0 ? "주의-준비됨" : "준비됨";
        var confidence = failed > 0 ? "낮음" : warnings > 0 ? "보통" : "높음";

        return new ReleaseGateResult(
            Version,
            status,
            confidence,
            $"배포 점검: 상태={status}, 실패={failed}, 주의={warnings}, 산출물={artifacts.Length}",
            gates,
            artifacts,
            BuildNextActions(status, gates));
    }

    private static ReleaseArtifact BuildArtifact(string name, string kind, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return new ReleaseArtifact(name, kind, path ?? "", false, 0, "", "없음", "파일이 없습니다.");
        }

        if (Directory.Exists(path))
        {
            return new ReleaseArtifact(name, kind, path, true, 0, "", "확인", "폴더 확인 완료");
        }

        var info = new FileInfo(path);
        return new ReleaseArtifact(
            name,
            kind,
            path,
            true,
            info.Length,
            HashFile(path),
            info.Length > 0 ? "확인" : "주의",
            info.Length > 0 ? "확인 완료" : "파일 크기가 0입니다.");
    }

    private static ReleaseGateItem CheckPath(string name, string category, string path, string severity)
    {
        var exists = !string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path));
        return new ReleaseGateItem(
            name,
            category,
            exists ? "통과" : "실패",
            severity,
            exists ? "확인 완료" : "배포 산출물에 포함되어야 합니다.",
            path ?? "");
    }

    private static ReleaseGateItem CheckDesktopFolder(string name, string category, string path, string severity)
    {
        try
        {
            Directory.CreateDirectory(path);
            return new ReleaseGateItem(name, category, "통과", severity, "폴더 준비 완료", path);
        }
        catch (Exception ex)
        {
            return new ReleaseGateItem(name, category, "실패", severity, ex.Message, path);
        }
    }

    private static IReadOnlyList<string> BuildNextActions(string status, IReadOnlyList<ReleaseGateItem> gates)
    {
        var actions = new List<string>();

        if (status == "준비됨")
        {
            actions.Add("포터블 ZIP, 설치 패키지, 서명 결과를 함께 보관하세요.");
            actions.Add("최종 잠금 확인에서 문서와 배포 점검을 고정하세요.");
            return actions;
        }

        foreach (var gate in gates.Where(x => x.Status != "통과").Take(8))
        {
            actions.Add($"{gate.Name}: {gate.Message}");
        }

        actions.Add("Windows 11 환경에서 publish-consumer → build-wix-msi → build-msix 순서로 다시 실행하세요.");
        return actions;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string FindDistRoot()
    {
        var root = FindProjectRoot();
        return Path.Combine(root, "dist");
    }

    private static string FindInstallerArtifact(string pattern)
    {
        var dir = Path.Combine(FindProjectRoot(), "artifacts", "installer");
        if (!Directory.Exists(dir)) return "";
        return Directory.GetFiles(dir, pattern).FirstOrDefault() ?? "";
    }

    private static string FindProjectFile(string relativePath)
    {
        return Path.Combine(FindProjectRoot(), relativePath);
    }

    private static string FindProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "README.md")) && Directory.Exists(Path.Combine(dir, "src")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return AppContext.BaseDirectory;
    }
}