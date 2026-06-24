namespace SmartPerformanceDoctor.Contracts.Services;

/// <summary>
/// 점검 범위(scope)별 허용 복구 작업 게이트. 시스템 점검에서 드라이버·오디오 작업을 차단합니다.
/// </summary>
public static class ScopeRepairFilter
{
    private static readonly HashSet<string> SystemActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "dism_checkhealth",
        "sfc_verifyonly"
    };

    private static readonly HashSet<string> DriverActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "driver_repair_plan_only",
        "driver_check_problem_devices",
        "pnputil_scan_devices",
        "pnputil_restart_device"
    };

    private static readonly HashSet<string> AudioActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio_repair_plan_only",
        "restart_audiosrv",
        "restart_audioendpointbuilder",
        "audio_restart_stack",
        "audio_scan_devices"
    };

    private static readonly Dictionary<string, HashSet<string>> ScopeModules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["quick"] = new(StringComparer.OrdinalIgnoreCase) { "quick" },
        ["system"] = new(StringComparer.OrdinalIgnoreCase) { "system" },
        ["driver"] = new(StringComparer.OrdinalIgnoreCase) { "driver" },
        ["audio"] = new(StringComparer.OrdinalIgnoreCase) { "audio" },
        ["full"] = new(StringComparer.OrdinalIgnoreCase) { "quick", "system", "driver", "audio" }
    };

    public static IReadOnlyList<string> ResolveModuleIds(string scope) => scope switch
    {
        "quick" => ["quick"],
        "system" => ["system"],
        "driver" => ["driver"],
        "audio" => ["audio"],
        "full" => ["quick", "system", "driver", "audio"],
        _ => ["quick"]
    };

    public static bool IsModuleAllowedForScope(string moduleId, string scope)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return false;
        }

        return ScopeModules.TryGetValue(scope, out var allowed) && allowed.Contains(moduleId);
    }

    public static bool IsAllowedForScope(string actionId, string scope)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return false;
        }

        return scope switch
        {
            "system" => SystemActions.Contains(actionId),
            "driver" => DriverActions.Contains(actionId),
            "audio" => AudioActions.Contains(actionId),
            "quick" => SystemActions.Contains(actionId)
                || DriverActions.Contains(actionId)
                || AudioActions.Contains(actionId),
            "full" => SystemActions.Contains(actionId)
                || DriverActions.Contains(actionId)
                || AudioActions.Contains(actionId),
            _ => DriverActions.Contains(actionId) || AudioActions.Contains(actionId)
        };
    }

    public static IReadOnlyList<string> FilterForScope(IEnumerable<string> actionIds, string scope) =>
        actionIds.Where(id => IsAllowedForScope(id, scope)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> DefaultActionsForScope(string scope) => scope switch
    {
        "driver" => ["driver_check_problem_devices", "pnputil_scan_devices"],
        "audio" => ["audio_scan_devices", "audio_restart_stack"],
        "system" => ["dism_checkhealth", "sfc_verifyonly"],
        "quick" => ["driver_check_problem_devices", "audio_scan_devices"],
        "full" => ["dism_checkhealth", "sfc_verifyonly", "driver_check_problem_devices", "audio_scan_devices", "pnputil_scan_devices", "audio_restart_stack"],
        _ => ["driver_check_problem_devices", "audio_scan_devices"]
    };
}