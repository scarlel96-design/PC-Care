using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services.Installation;
using SmartPerformanceDoctor.Contracts.Services.Installation;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class FeatureManagementPage : Page
{
    private readonly InstalledFeaturesService _features = new();

    public FeatureManagementPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var manifest = _features.Load();
        SummaryText.Text =
            $"버전 {manifest.Version} · 설치 모드 {manifest.InstallMode} · 설치 시각 {manifest.InstalledAt}";

        FeatureList.ItemsSource = FeatureCatalog.All
            .Select(f => new
            {
                Line = $"{(manifest.IsEnabled(f.Id) ? "✓" : "·")} {f.DisplayName} — {f.Description}",
                Enabled = manifest.IsEnabled(f.Id)
            })
            .OrderByDescending(x => x.Enabled)
            .ThenBy(x => x.Line)
            .Select(x => x.Line)
            .ToList();
    }

    private async void OpenInstaller(object sender, RoutedEventArgs e)
    {
        var setup = ResolveSetupPath();
        if (setup is null)
        {
            SummaryText.Text = "설치 관리자를 찾을 수 없습니다.";
            var dialog = new ContentDialog
            {
                Title = "설치 관리자 없음",
                Content = "SmartPerformanceDoctor.Setup.exe 또는 SmartPerformanceDoctor_설치.exe를 찾지 못했습니다.\n\n" +
                          "다음 위치를 확인하세요:\n" +
                          "• 프로그램 설치 폴더\n" +
                          "• artifacts\\installer\\layout\n" +
                          "• 상위 폴더의 SmartPerformanceDoctor_설치.exe",
                CloseButtonText = "확인",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(setup)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = "--modify"
            });
            SummaryText.Text = $"설치 관리자 실행: {setup}";
        }
        catch (Exception ex)
        {
            SummaryText.Text = $"설치 관리자 실행 실패: {ex.Message}";
            var dialog = new ContentDialog
            {
                Title = "실행 실패",
                Content = ex.Message,
                CloseButtonText = "확인",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private static string? ResolveSetupPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDir, "SmartPerformanceDoctor.Setup.exe"),
            Path.Combine(baseDir, "SmartPerformanceDoctor_설치.exe"),
            Path.Combine(baseDir, "..", "SmartPerformanceDoctor_설치.exe"),
            Path.Combine(baseDir, "..", "installer", "layout", "SmartPerformanceDoctor.Setup.exe"),
            Path.Combine(baseDir, "..", "artifacts", "installer", "layout", "SmartPerformanceDoctor.Setup.exe"),
        };

        var setupDir = Path.Combine(baseDir, "..", "installer", "setup");
        if (Directory.Exists(setupDir))
        {
            candidates.AddRange(Directory.EnumerateFiles(setupDir, "SmartPerformanceDoctor_Setup_v*.exe"));
            candidates.Add(Path.Combine(setupDir, "SmartPerformanceDoctor_Setup.exe"));
        }

        var layoutDir = Path.Combine(baseDir, "..", "artifacts", "installer", "layout");
        if (Directory.Exists(layoutDir))
        {
            candidates.Add(Path.Combine(layoutDir, "SmartPerformanceDoctor.Setup.exe"));
            candidates.AddRange(Directory.EnumerateFiles(layoutDir, "SmartPerformanceDoctor_설치.exe"));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }
}