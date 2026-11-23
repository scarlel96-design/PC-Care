# REA installation

The independent npm runtime uses rea-agents 6.7.0. Its lockfile is under tools/BenchmarkSamples/rea-runtime. The setup preview identified two changes: Codex client registration and skill installation. The retained setup receipt records both changes. It includes a planned client backup. The existing registration was reused on resume.

The general doctor reported unavailable optional dependencies. The client and MCP checks passed at setup. The skill check did not pass. No private config, backup or dependency tree is published. Source-copy rollback does not undo host settings.
