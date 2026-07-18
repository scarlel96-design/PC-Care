namespace SmartPerformanceDoctor.App.Services;

/// <summary>
/// Unpackaged self-contained WinUI needs the Microsoft.UI.Xaml resource map (PRI) beside the exe.
/// Publish output often has Microsoft.UI.Xaml.Controls.pri only; XamlControlsResources resolves via Microsoft.UI.Xaml.
/// </summary>
public static class WinUiResourceLayoutGuard
{
    private const string ResourceMapPri = "Microsoft.UI.Xaml.pri";
    private const string ControlsPri = "Microsoft.UI.Xaml.Controls.pri";

    public static void EnsureOrThrow()
    {
        var root = RuntimePaths.InstallRoot;
        var mapPri = Path.Combine(root, ResourceMapPri);
        if (File.Exists(mapPri))
        {
            return;
        }

        var controlsPri = Path.Combine(root, ControlsPri);
        if (!File.Exists(controlsPri))
        {
            throw new FileNotFoundException(
                $"WinUI 리소스(PRI)가 설치 폴더에 없습니다: {ControlsPri}. 설치 프로그램으로 복구하거나 재설치하세요.");
        }

        File.Copy(controlsPri, mapPri, overwrite: true);
    }
}