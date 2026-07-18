using SmartPerformanceDoctor.AstraVault.Legacy;
using SmartPerformanceDoctor.AstraVault.Session;
using SmartPerformanceDoctor.App.Models.Security;

namespace SmartPerformanceDoctor.App.Services.Security;

/// <summary>
/// 아스트라 금고 통합 진입점. 레거시 spd-vault는 <see cref="SecureVaultService"/>,
/// 신규 AVLT v3는 추후 <c>AstraV3VaultBackend</c>로 위임 (Phase E+).
/// </summary>
public sealed class AstraVaultHostService : IDisposable
{
    private readonly SecureVaultService _legacy = new();

    public string ProductName => "아스트라 금고";
    public string ProductSubtitle => "Astra Vault — 암호화 컨테이너 금고";

    public DetectedVaultKind DetectedKind => LegacyVaultInventory.Detect(SecureVaultPaths.Root);

    public VaultSecurityState SecurityState => MapState(_legacy.State);

    public SecureVaultService Legacy => _legacy;

    public static VaultSecurityState MapState(SecureVaultState state) =>
        state switch
        {
            SecureVaultState.NotCreated => VaultSecurityState.NotCreated,
            SecureVaultState.Locked => VaultSecurityState.Locked,
            SecureVaultState.Unlocked => VaultSecurityState.Unlocked,
            _ => VaultSecurityState.Locked
        };

    public void Dispose() => _legacy.Dispose();
}