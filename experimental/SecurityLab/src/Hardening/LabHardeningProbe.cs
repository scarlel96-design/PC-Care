using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>
/// Legitimate integrity / environment probe. Warn-only — no anti-AV, no process hiding.
/// </summary>
public static class LabHardeningProbe
{
    public sealed class Report
    {
        public bool DebuggerAttached { get; init; }
        public bool Is64BitProcess { get; init; }
        public string Runtime { get; init; } = "";
        public IReadOnlyList<string> Recommendations { get; init; } = Array.Empty<string>();
        public string Summary { get; init; } = "";
    }

    public static Report Probe()
    {
        var dbg = Debugger.IsAttached || IsDebuggerPresentNative();
        var recs = new List<string>
        {
            "Release 빌드에서 심볼을 제거(strip)하고 Authenticode 서명을 적용하세요.",
            "금고 경로를 OneDrive 등 동기화 폴더에 두지 않는 것을 권장합니다.",
            "잠금 해제 세션 시간을 최소화하고 유휴 시 잠그세요.",
            "마이그레이션 후 임시 폴더가 남지 않았는지 확인하세요."
        };
        if (dbg)
        {
            recs.Insert(0, "디버거가 연결되어 있습니다. 민감 작업 전 세션 잠금을 권장합니다. (강제 종료하지 않음)");
        }

        return new Report
        {
            DebuggerAttached = dbg,
            Is64BitProcess = Environment.Is64BitProcess,
            Runtime = RuntimeInformation.FrameworkDescription,
            Recommendations = recs,
            Summary = dbg
                ? "환경: 디버거 감지(경고만). 보안 기능은 계속 사용 가능."
                : "환경: 디버거 미감지. 기본 권고를 따르세요."
        };
    }

    private static bool IsDebuggerPresentNative()
    {
        try
        {
            return IsDebuggerPresent();
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool IsDebuggerPresent();
}
