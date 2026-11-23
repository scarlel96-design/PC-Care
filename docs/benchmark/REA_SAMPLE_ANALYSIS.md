# REA sample analysis

Build the Plain and Calls projects in Release. Run scripts/wo28-rea-validation.mjs with the locked REA package and a private output directory. It compares output to the synthetic source. Plain checks metadata. Calls checks Calculator.Evaluate calling Double. The JavaScript application checks module imports. Thirteen semantic unknowns remain. General JavaScript call resolution is unqualified.

Result: WO28_REA_SAMPLE_VALIDATION_PASS. Two managed binaries and one JavaScript application passed their stated checks. The CLI and stdio tests use the same inputs; do not count them as additional samples. No competitor application was an input.

To reproduce the CLI and independent MCP checks, run these commands from the repository root.
Use a dedicated output directory. Keep raw output private because it contains local paths.

~~~powershell
dotnet build tools/BenchmarkSamples/Plain -c Release
dotnet build tools/BenchmarkSamples/Calls -c Release
npm ci --prefix tools/BenchmarkSamples/rea-runtime --ignore-scripts --no-audit --no-fund
node scripts/wo28-rea-validation.mjs tools/BenchmarkSamples/rea-runtime/node_modules/rea-agents artifacts/wo28-private-samples
~~~

Check the final marker and exit code: WO28_REA_SAMPLE_VALIDATION_PASS, exit 0.
The independent MCP check closes its own server. Run inspect_managed_members from the active Codex session to qualify that separate connection.
