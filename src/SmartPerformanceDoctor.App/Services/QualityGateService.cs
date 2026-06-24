using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class QualityGateService
{
    public IReadOnlyList<QualityGateItem> Evaluate()
    {
        var baseDir = RuntimePaths.InstallRoot;
        var items = new List<QualityGateItem>();

        AddFileGate(items, "진단 엔진", RuntimePaths.ResolveEnginePath("smart_performance_doctor_core.exe"), "높음");
        AddFileGate(items, "복구 도우미", RuntimePaths.ResolveEnginePath("smart_performance_doctor_repair_helper.exe"), "높음");
        AddDirectoryGate(items, "진단 규칙", RuntimePaths.RulesDirectory, "보통");
        AddDirectoryGate(items, "디자인 리소스", RuntimePaths.AssetsDirectory, "보통");

        var reportRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "Reports");
        var repairLogRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "RepairLogs");

        AddDirectoryGate(items, "보고서 폴더", reportRoot, "낮음", createIfMissing: true);
        AddDirectoryGate(items, "복구 기록 폴더", repairLogRoot, "낮음", createIfMissing: true);

        items.Add(new QualityGateItem(
            "시스템 복구 안전장치",
            "정적 확인",
            "높음",
            "시스템 복구 정체 대응은 진단 엔진에 포함되어 있으며 실제 검증은 Windows 11에서 필요합니다."));

        items.Add(new QualityGateItem(
            "복구 안전 정책",
            "정적 확인",
            "높음",
            "복구 도우미는 허용 목록, 시뮬레이션/실행 분리, 위험 승인, 보안 핸드셰이크 구조를 사용합니다."));

        return items;
    }

    private static void AddFileGate(List<QualityGateItem> items, string name, string path, string severity)
    {
        var exists = File.Exists(path);
        items.Add(new QualityGateItem(
            name,
            exists ? "통과" : "실패",
            severity,
            exists ? $"파일 확인: {path}" : $"필수 파일 누락: {path}"));
    }

    private static void AddDirectoryGate(List<QualityGateItem> items, string name, string path, string severity, bool createIfMissing = false)
    {
        if (!Directory.Exists(path) && createIfMissing)
        {
            try { Directory.CreateDirectory(path); } catch { }
        }

        var exists = Directory.Exists(path);
        items.Add(new QualityGateItem(
            name,
            exists ? "통과" : "주의",
            severity,
            exists ? $"폴더 확인: {path}" : $"폴더 누락: {path}"));
    }
}