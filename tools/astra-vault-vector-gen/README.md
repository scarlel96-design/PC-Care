# astra-vault-vector-gen (TEST ONLY)

- **Not** included in PCCare release packages.
- Does **not** reference `SmartPerformanceDoctor.AstraVault` or production writers/parsers.
- Reads only `test-vectors/av3/reference-input.json` (deterministic fixtures).
- Writes only under `test-vectors/av3/reference-output/`.

```bash
dotnet run --project tools/astra-vault-vector-gen/AstraVaultVectorGen.csproj -- \
  "<repo-root>/test-vectors/av3"
```