# WO03 elevated helper

`SmartPerformanceDoctor.UpdateHelper` accepts only `--apply <package> --wait-pid <PID>` and `--recover`. It requires Windows administrator membership, derives the installation directory from its own `engine` location below Program Files, reads the installed `PCCare.exe` file version and uses the production publisher policy. It has no command execution, script, arbitrary target-root or user-supplied output verb.

For apply, the helper first recovers an interrupted transaction. If recovery restored files, it stops for review before another attempt. It waits up to 90 seconds for the caller process to exit, aborts on timeout, verifies the package again inside the protected transaction directory, applies only signed allowlisted files, checks installed version and required components, and returns a nonzero status on failure. The desktop app starts it through ShellExecute `runas` after a successful reinspection and exits only after process launch succeeds.

The legacy PowerShell recursive-copy entry point remains disabled. The existing repair helper has a different privilege boundary and was not merged into this binary. The current production policy has no trusted public key, so an in-app production apply cannot pass publisher verification. UAC accept/deny and installed-build recovery remain E2E gates.
