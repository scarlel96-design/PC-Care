using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public static class AppNavigationService
{
    public static void Navigate(Frame frame, Type pageType, object? parameter = null)
    {
        if (frame.CurrentSourcePageType == pageType && parameter is null)
        {
            return;
        }

        if (frame.Content is Views.ModulePage modulePage &&
            pageType == typeof(Views.ModulePage) &&
            TryCreateModuleRequest(parameter, out var moduleRequest))
        {
            modulePage.ApplyNavigation(moduleRequest);
            return;
        }

        frame.Navigate(pageType, parameter);
    }

    public static void NavigateDashboard(Frame frame)
    {
        Navigate(frame, typeof(Views.MacDashboardPage));
    }

    public static void NavigateUnifiedCare(
        Frame frame,
        string scope = "quick",
        bool autoStart = false,
        bool includeRepair = false,
        bool riskAccepted = false)
    {
        var request = new CareNavigationRequest(scope, autoStart, includeRepair, riskAccepted);
        if (frame.Content is Views.UnifiedCarePage existing)
        {
            existing.ApplyNavigation(request);
            return;
        }

        Navigate(frame, typeof(Views.UnifiedCarePage), request);
    }

    public static void NavigateModule(Frame frame, string moduleId, bool autoRun = false)
    {
        Navigate(frame, typeof(Views.UnifiedCarePage), new CareNavigationRequest(moduleId, autoRun));
    }

    public static bool TryNavigateByName(Frame frame, string targetPage, object? parameter = null)
    {
        if (targetPage.Contains(':', StringComparison.Ordinal))
        {
            var parts = targetPage.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && parts[0] is "ModulePage" or "UnifiedCarePage")
            {
                var scope = parts[1];
                var autoStart = scope is not ("driver" or "audio");
                NavigateUnifiedCare(frame, scope, autoStart);
                return true;
            }
        }

        var pageType = targetPage switch
        {
            "MacDashboardPage" or "DashboardPage" => typeof(Views.MacDashboardPage),
            "UnifiedCarePage" => typeof(Views.UnifiedCarePage),
            "ModulePage" => typeof(Views.UnifiedCarePage),
            "DriverRepairPage" => typeof(Views.UnifiedCarePage),
            "AudioRepairPage" => typeof(Views.UnifiedCarePage),
            "SettingsPage" => typeof(Views.SettingsPage),
            "AdvancedCenterPage" => typeof(Views.AdvancedCenterPage),
            "SystemCareCenterPage" => typeof(Views.SystemCareCenterPage),
            "SecureVaultCenterPage" => typeof(Views.SecureVaultCenterPage),
            "SecureDeleteCenterPage" => typeof(Views.SecureDeleteCenterPage),
            "KnowledgePackManagerPage" => typeof(Views.KnowledgePackManagerPage),
            "ProtocolCenterPage" => typeof(Views.ProtocolCenterPage),
            "ProgramProtectionCenterPage" => typeof(Views.ProgramProtectionCenterPage),
            "IntelligenceCenterPage" => typeof(Views.IntelligenceCenterPage),
            "DeepScanSetupPage" => typeof(Views.DeepScanSetupPage),
            "UpdateStatusPage" => typeof(Views.UpdateStatusPage),
            "StableLogLayoutPage" => typeof(Views.StableLogLayoutPage),
            "EvidenceExplorerPage" => typeof(Views.EvidenceExplorerPage),
            "FeatureManagementPage" => typeof(Views.FeatureManagementPage),
            "FirstRunPage" => typeof(Views.FirstRunPage),
            "ReleaseStatusPage" => typeof(Views.ReleaseStatusPage),
            "ReleaseArtifactGatePage" => typeof(Views.ReleaseArtifactGatePage),
            "FinalLockPage" => typeof(Views.FinalLockPage),
            "SelfHealingPage" => typeof(Views.SelfHealingPage),
            "VerifiedRepairPage" => typeof(Views.VerifiedRepairPage),
            "ErrorBundlePage" => typeof(Views.ErrorBundlePage),
            "AppDiagnosticsPage" => typeof(Views.AppDiagnosticsPage),
            "ReportPage" => typeof(Views.ReportPage),
            "RepairLogPage" => typeof(Views.RepairLogPage),
            "CrashLogPage" => typeof(Views.CrashLogPage),
            "RiskGatePage" => typeof(Views.RiskGatePage),
            "RepairHelperE2EGatePage" => typeof(Views.RepairHelperE2EGatePage),
            "ProgressHudPage" => typeof(Views.ProgressHudPage),
            _ => null
        };

        if (pageType is null)
        {
            return false;
        }

        Navigate(frame, pageType, parameter);
        return true;
    }

    public static void NavigateCard(Frame frame, string cardId)
    {
        switch (cardId)
        {
            case "system":
                NavigateUnifiedCare(frame, "system", autoStart: true);
                break;
            case "driver":
                NavigateUnifiedCare(frame, "driver", autoStart: false);
                break;
            case "audio":
                NavigateUnifiedCare(frame, "audio", autoStart: false);
                break;
            case "reports":
                Navigate(frame, typeof(Views.ReportPage));
                break;
            case "repairlogs":
                Navigate(frame, typeof(Views.RepairLogPage));
                break;
            case "crashlogs":
                Navigate(frame, typeof(Views.CrashLogPage));
                break;
            default:
                NavigateDashboard(frame);
                break;
        }
    }

    private static bool TryCreateModuleRequest(object? parameter, out ModuleNavigationRequest request)
    {
        switch (parameter)
        {
            case ModuleNavigationRequest typed:
                request = typed;
                return true;
            case string moduleId:
                request = new ModuleNavigationRequest(moduleId);
                return true;
            default:
                request = new ModuleNavigationRequest("system");
                return parameter is not null;
        }
    }
}