namespace SmartPerformanceDoctor.AstraVault.RiskClosure.PartialWrite;

/// <summary>AV3 on-disk artifact boundaries for torn/partial write FI (test-only harness).</summary>
public enum Av3WriteBoundary
{
    Locator = 1,
    HeaderCopy = 2,
    ActivationPayload = 3,
    MetadataRootCiphertext = 4,
    JournalDescriptor = 5,
    ObjectPlaceholder = 6
}