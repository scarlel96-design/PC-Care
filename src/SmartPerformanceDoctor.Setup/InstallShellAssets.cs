using System.IO;

namespace SmartPerformanceDoctor.Setup;

internal static class InstallShellAssets
{
    private static readonly string[] RequiredRootXbf = ["App.xbf", "MainWindow.xbf"];

    public static void EnsureFromLayout(string layoutDir, string targetDir)
    {
        foreach (var name in RequiredRootXbf)
        {
            var source = Path.Combine(layoutDir, name);
            if (!File.Exists(source))
            {
                throw new InvalidOperationException($"설치 레이아웃에 필수 WinUI 리소스가 없습니다: {name}");
            }

            var dest = Path.Combine(targetDir, name);
            InstallFileOperations.CopyFile(source, dest);
        }
    }

    public static void EnsurePresentOrThrow(string targetDir)
    {
        foreach (var name in RequiredRootXbf)
        {
            var path = Path.Combine(targetDir, name);
            if (!File.Exists(path) || new FileInfo(path).Length < 16)
            {
                throw new InvalidOperationException(
                    $"설치 후 필수 화면 리소스가 누락되었습니다: {name}. " +
                    "%LocalAppData%\\PCCare\\installer-cache 를 삭제한 뒤 설치 프로그램을 다시 실행하세요.");
            }
        }
    }
}
