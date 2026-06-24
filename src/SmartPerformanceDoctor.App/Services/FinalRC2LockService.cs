using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class FinalRC2LockService
{
    public const string Version = "44.0.0";

    public FinalLockResult Evaluate()
    {
        var gates = new List<FinalLockGateItem>
        {
            CheckFile("디자인 토큰", "ui", "src/SmartPerformanceDoctor.App/Resources/MacDesignTokens.xaml", "높음"),
            CheckFile("XAML 대체 리소스", "ui", "src/SmartPerformanceDoctor.App/Resources/MacDesignFallback.xaml", "높음"),
            CheckFile("실행 기록 레이아웃", "ui", "src/SmartPerformanceDoctor.App/Views/StableLogLayoutPage.xaml", "높음"),
            CheckFile("홈 화면 분석", "dashboard", "src/SmartPerformanceDoctor.App/Services/DashboardIntelligenceService.cs", "높음"),
            CheckFile("엔진 연동", "dashboard", "src/SmartPerformanceDoctor.App/Services/CoreDashboardBridgeService.cs", "높음"),
            CheckFile("진행 상황 스트림", "runtime", "src/SmartPerformanceDoctor.App/Services/OperationProgressHub.cs", "높음"),
            CheckFile("복구 검증 엔진", "repair", "src/SmartPerformanceDoctor.App/Services/RepairVerificationEngine.cs", "높음"),
            CheckFile("복구 품질 점검", "repair", "src/SmartPerformanceDoctor.App/Services/RepairHelperE2EGateService.cs", "높음"),
            CheckFile("배포 검증", "release", "src/SmartPerformanceDoctor.App/Services/ReleaseArtifactVerificationService.cs", "높음"),
            CheckFile("최종 잠금 페이지", "final", "src/SmartPerformanceDoctor.App/Views/FinalLockPage.xaml", "보통"),
            CheckFile("최종 잠금 스크립트", "final", "scripts/run-final-rc2-lock.ps1", "높음"),
            CheckFile("소비자 배포 스크립트", "release", "scripts/publish-consumer.ps1", "높음"),
            CheckFile("WiX 빌드 스크립트", "release", "scripts/build-wix-msi.ps1", "보통"),
            CheckFile("MSIX 빌드 스크립트", "release", "scripts/build-msix.ps1", "보통"),
            CheckFile("서명 스크립트", "release", "scripts/sign-consumer.ps1", "보통")
        };

        var failed = gates.Count(x => x.Status == "실패");
        var warnings = gates.Count(x => x.Status == "주의");
        var status = failed > 0 ? "미잠금" : warnings > 0 ? "수동확인-잠금" : "잠금됨";
        var confidence = failed > 0 ? "낮음" : warnings > 0 ? "보통" : "높음";

        return new FinalLockResult(
            Version,
            status,
            confidence,
            $"최종 잠금: 상태={status}, 실패={failed}, 주의={warnings}, 항목={gates.Count}",
            gates,
            AcceptanceCriteria(),
            RemainingManualChecks());
    }

    private static FinalLockGateItem CheckFile(string name, string category, string relativePath, string severity)
    {
        var root = FindProjectRoot();
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var exists = File.Exists(path);

        return new FinalLockGateItem(
            name,
            category,
            exists ? "통과" : "실패",
            severity,
            exists ? "소스 항목 확인 완료" : "최종 잠금에 필요한 항목이 없습니다.",
            path);
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

    private static IReadOnlyList<string> AcceptanceCriteria()
    {
        return
        [
            "홈 화면은 이용자 친화 한국어 문구를 유지",
            "보존 실행 로그는 실행 기록 페이지에서만 표시",
            "드라이버/오디오/시스템 복구는 시뮬레이션 우선 원칙 유지",
            "실제 복구는 위험 승인 절차를 통과해야 함",
            "복구 품질 점검 실행 가능",
            "포터블/WiX/MSIX 배포 스크립트 존재",
            "서명 스크립트 및 개발용 인증서 생성 스크립트 존재",
            "오류 보고서/오류 기록/복구 감사 수집 경로 유지",
            "시스템 복구 안전장치 유지",
            "Windows 11 최종 검증 절차 문서화"
        ];
    }

    private static IReadOnlyList<string> RemainingManualChecks()
    {
        return
        [
            "Windows 11에서 dotnet restore/build/publish 실행",
            "진단·복구 엔진 cargo check/build 실행",
            "앱 실행 후 UI 렌더링 확인",
            "실행 기록 페이지 스크롤 시 글자 겹침 없음 확인",
            "검증형 복구 페이지 시뮬레이션 확인",
            "복구 품질 점검 확인",
            "publish-consumer.ps1 실행",
            "실제 인증서 환경에서 서명 확인"
        ];
    }
}