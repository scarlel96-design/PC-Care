using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services.Installation;
using SmartPerformanceDoctor.Contracts.Models.Installation;

namespace SmartPerformanceDoctor.App.Services;

public static class FeatureVisibilityService
{
    public static bool IsNavVisible(
        string? navTag,
        InstalledFeaturesService features,
        UserModeService userMode)
    {
        if (string.IsNullOrWhiteSpace(navTag))
        {
            return true;
        }

        if (navTag.StartsWith("mode:", StringComparison.OrdinalIgnoreCase))
        {
            var required = ParseMode(navTag["mode:".Length..]);
            return userMode.Meets(required);
        }

        if (navTag.StartsWith("feature:", StringComparison.OrdinalIgnoreCase))
        {
            var featureId = navTag["feature:".Length..];
            return NavigationFeatureMap.ShouldShow(featureId, features);
        }

        if (navTag.StartsWith("nav:", StringComparison.OrdinalIgnoreCase))
        {
            var navId = navTag["nav:".Length..];
            return navId switch
            {
                "home" or "settings" => true,
                "pc-check" => NavigationFeatureMap.ShouldShow(InstallFeatureIds.CoreDiagnostics, features),
                "reports" or "activity" => NavigationFeatureMap.ShouldShow(InstallFeatureIds.ReportAudit, features),
                "feature-mgmt" => NavigationFeatureMap.ShouldShow(InstallFeatureIds.ConfigManager, features),
                "advanced-center" => userMode.Meets(UserMode.Advanced),
                "system-care" or "secure-vault" or "secure-delete" => true,
                "knowledge-pack" => userMode.Meets(UserMode.Advanced)
                    && NavigationFeatureMap.ShouldShow(InstallFeatureIds.KnowledgePack, features),
                "program-protection" => userMode.Meets(UserMode.Advanced)
                    && NavigationFeatureMap.ShouldShow(InstallFeatureIds.ProgramIntegrity, features),
                "app-diagnostics" or "progress-hud" => userMode.Meets(UserMode.Advanced),
                "update" => userMode.Meets(UserMode.Advanced)
                    && NavigationFeatureMap.ShouldShow(InstallFeatureIds.UpdateManifest, features),
                "stable-logs" or "crash-logs" or "repair-logs" => userMode.Meets(UserMode.Advanced),
                "intelligence" or "protocol" or "deep-scan" => userMode.Meets(UserMode.Advanced),
                "release-status" or "release-gate" or "final-lock" or "repair-e2e" or "verified-repair"
                    or "risk-gate" or "self-healing" or "error-bundle" or "first-run" => userMode.Meets(UserMode.Developer),
                _ => userMode.Meets(UserMode.Basic)
            };
        }

        return true;
    }

    public static bool IsSectionVisible(string sectionTag, UserModeService userMode) =>
        sectionTag switch
        {
            "section:primary" or "section:history" or "section:settings" => true,
            "section:advanced-inline" => userMode.Meets(UserMode.Advanced),
            _ => true
        };

    private static UserMode ParseMode(string value) =>
        Enum.TryParse<UserMode>(value, true, out var mode) ? mode : UserMode.Basic;
}