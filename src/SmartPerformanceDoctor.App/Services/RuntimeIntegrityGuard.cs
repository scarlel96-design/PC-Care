using System.Runtime.InteropServices;

namespace SmartPerformanceDoctor.App.Services;

public static class RuntimeIntegrityGuard
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
    private const long MinMainDllBytes = 65536;

    public static string? ValidateOrGetError()
    {
        var root = RuntimePaths.InstallRoot;
        var mainDll = Path.Combine(root, "SmartPerformanceDoctor.dll");
        var mainExe = SmartPerformanceDoctor.Aegis.AppExecutableResolver.ResolveMainExecutable(root);
        var bootstrap = Path.Combine(root, "runtimes", "win-x64", "native", "Microsoft.WindowsAppRuntime.Bootstrap.dll");

        if (mainExe is null)
        {
            return "메인 실행 파일(PCCare.exe / AstraCare.exe / SmartPerformanceDoctor.exe)이 없습니다. 설치를 복구하거나 scripts\\build.ps1 로 다시 배포하세요.";
        }

        if (!File.Exists(mainDll) || new FileInfo(mainDll).Length < MinMainDllBytes)
        {
            return "프로그램 파일이 손상되었습니다. 설치를 복구하거나 scripts\\build.ps1 로 다시 배포하세요.";
        }

        if (!File.Exists(bootstrap))
        {
            return "Windows App SDK 런타임 파일이 누락되었습니다. 전체 설치 패키지로 복구하세요.";
        }

        return null;
    }

    public static void EnsureOrExit()
    {
        var error = ValidateOrGetError();
        if (!string.IsNullOrWhiteSpace(error))
        {
            _ = MessageBox(IntPtr.Zero, error, AppInfo.ProductName, 0x10);
            Environment.Exit(1);
        }

        RuntimeTrustState.Initialize();
        if (RuntimeTrustState.IsFullyTrusted)
        {
            return;
        }

        var unsigned = RuntimeTrustState.UnsignedFiles;
        var detail = unsigned.Count == 0
            ? RuntimeTrustState.Summary
            : string.Join(", ", unsigned.Take(3));
        _ = MessageBox(
            IntPtr.Zero,
            "일부 실행 파일의 코드 서명을 확인하지 못했습니다. " +
            "고위험 기능(보안 삭제·보안 금고·장치 복구 등)은 비활성화되고 기본 진단만 사용할 수 있습니다.\n\n" +
            $"상태: {RuntimeTrustState.Summary}\n{detail}",
            AppInfo.ProductName,
            0x30);
    }
}