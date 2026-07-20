namespace SmartPerformanceDoctor.SecurityLab.ProductBridge;

/// <summary>
/// Product-side flag contract. Defaults OFF until product host binds them
/// (App 50.3.0+ calls <see cref="EnableProductHost"/> at startup).
/// Lab unit tests leave host unbound so flags stay false.
/// </summary>
public static class ProductFeatureFlags
{
    private static volatile bool _hostEnabled;
    private static volatile bool _vaultV4 = true;
    private static volatile bool _shredNext = true;
    private static volatile bool _migrate = true;

    /// <summary>Called once from product App OnLaunched / ctor for 50.3.0 ship.</summary>
    public static void EnableProductHost(
        bool master = true,
        bool vaultV4 = true,
        bool shredNext = true,
        bool migrate = true)
    {
        _hostEnabled = master;
        _vaultV4 = vaultV4;
        _shredNext = shredNext;
        _migrate = migrate;
    }

    /// <summary>Test/dev: force all off again.</summary>
    public static void ResetForTests()
    {
        _hostEnabled = false;
        _vaultV4 = true;
        _shredNext = true;
        _migrate = true;
    }

    public static bool SecurityLabEnabled => _hostEnabled;

    public static bool VaultV4UiEnabled => _hostEnabled && _vaultV4;
    public static bool ShredNextEnabled => _hostEnabled && _shredNext;
    public static bool MigrationUiEnabled => _hostEnabled && _migrate;

    public static string StatusSummary =>
        $"SecurityLabEnabled={SecurityLabEnabled}; VaultV4={VaultV4UiEnabled}; " +
        $"ShredNext={ShredNextEnabled}; Migration={MigrationUiEnabled}";
}
