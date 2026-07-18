namespace SmartPerformanceDoctor.SecurityLab.ProductBridge;

/// <summary>
/// Product merge entry gate. All product adapters must call EnsureEnabled() first.
/// With default flags (all false) this always throws — product remains on v3 path.
/// </summary>
public static class LabProductGate
{
    public static void EnsureEnabled(string feature)
    {
        if (!ProductFeatureFlags.SecurityLabEnabled)
        {
            throw new InvalidOperationException(
                $"SecurityLab 제품 게이트 차단: master flag OFF ({feature}). 승인된 병합 전 호출 금지.");
        }

        var ok = feature.ToLowerInvariant() switch
        {
            "vault" or "vaultv4" => ProductFeatureFlags.VaultV4UiEnabled,
            "shred" or "shrednext" => ProductFeatureFlags.ShredNextEnabled,
            "migrate" or "migration" => ProductFeatureFlags.MigrationUiEnabled,
            _ => false
        };

        if (!ok)
        {
            throw new InvalidOperationException(
                $"SecurityLab 기능 플래그 OFF: {feature}. 제품은 안정 v3 경로를 사용해야 합니다.");
        }
    }

    public static bool IsFeatureVisible(string feature)
    {
        try
        {
            EnsureEnabled(feature);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
