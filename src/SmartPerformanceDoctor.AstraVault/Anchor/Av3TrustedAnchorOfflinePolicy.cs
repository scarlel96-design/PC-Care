using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Offline grace posture for hybrid trusted anchor (no writer promotion offline).</summary>
public static class Av3TrustedAnchorOfflinePolicy
{
    public static bool WriterTrustedPromotionRequiresOnlineExternalConfirmation =>
        !Av3PhaseGate.E131TrustedAnchorSignoffGateComplete || Av3PhaseGate.E13TrustedAnchorProviderPackageComplete;

    public const Av3TrustedAnchorOfflineState DefaultOfflinePosture =
        Av3TrustedAnchorOfflineState.OfflineGraceReadOnly;

    public static bool AllowsWriterTrustedPromotion(Av3TrustedAnchorOfflineState state) =>
        state == Av3TrustedAnchorOfflineState.Online;
}