# WO-12 open-source evaluation

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

## Selection

| Technology | Version / license | Decision | Compatibility and evidence |
|---|---|---|---|
| .NET cryptography | .NET 10.0.401 SDK; existing platform APIs | Reuse AesGcm, ChaCha20Poly1305, HKDF and RNG | Release build and real synthetic round trips |
| Konscious.Security.Cryptography.Argon2 | Lab 1.3.0; app central 1.3.1; MIT | Retain existing Argon2id format | KDF/round-trip regression; no version migration |
| ProtectedData | Lab 9.0.0; app central 10.0.10 | Retain existing dependency | No new key-store format |
| Windows CNG | Windows platform API | Evaluate; no new provider | Existing .NET primitives provide the required current path |
| libsodium | Official documentation lists 1.0.22-stable; ISC | Evaluate; not adopted | Native binary and binding review, packaging and format compatibility remain untested |
| WinUI 3 / Windows App SDK | Existing repository version | Reuse ListView virtualization and design tokens | XAML build; GUI NOT TESTED |
| Microsoft UI Automation | Windows platform API | Extend existing GuiProbe | Probe build PASS; account gate BLOCKED; live inspection NOT TESTED |
| Existing MVVM | Repository ViewModel conventions | Reuse | No new MVVM package or duplicate state engine |

## Security and maintenance

The official libsodium documentation confirms Windows support and maintained release distribution.
Its adoption requires binding-specific review. Native-library security does not validate an arbitrary .NET binding.
The NuGet advisory query returned no listed vulnerable Lab packages, exit 0.
This result is a feed snapshot. It does not prove absence of vulnerabilities or external cryptographic audit.
Current libraries compiled and ran on .NET 10. No unsupported compatibility claim was inferred from version numbers.
Konscious maintenance and advisory coverage require continuing review.
Existing custom HChaCha20 remains an explicit review item. It was not replaced during this compatibility-preserving patch.

## Sources checked

- [.NET AesGcm contract](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-10.0)
- [libsodium official documentation](https://libsodium.gitbook.io/doc/)
- [Konscious official repository](https://github.com/kmaragon/Konscious.Security.Cryptography)
- [WinUI ListView and GridView](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/listview-and-gridview)
- [Windows CNG](https://learn.microsoft.com/en-us/windows/win32/seccng/cng-portal)

No new cryptographic package was adopted. Existing APIs and the verified file-boundary implementation were reused.
