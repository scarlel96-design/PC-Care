using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App;

public static class AppInfo
{
    public const string ProductName = AstraCareBranding.ProductFormal;
    public const string ProductNameEnglish = AstraCareBranding.Product;
    public const string BuildVersion = "50.0.0";
    public static string Version => AppVersionService.GetInstalledVersion();
    public const string Channel = "stable";
    public const string SupportTagline = AstraCareBranding.TaglineKorean;
    public const string Description = AstraCareBranding.Description;
}