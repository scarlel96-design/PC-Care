using Xunit;

namespace SmartPerformanceDoctor.Tests;

// Both fixtures replace the process-wide vault-root variable and product gates.
// Serialize them so one fixture cannot redirect or remove another fixture's vault.
[CollectionDefinition("VaultEnvironment", DisableParallelization = true)]
public sealed class VaultEnvironmentCollection
{
}
