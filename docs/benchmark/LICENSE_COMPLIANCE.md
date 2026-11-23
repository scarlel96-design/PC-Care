# WO28 - License and reuse record

Observed: 2026-10-19 UTC. PC-Care source: `620ca720a192a5fdb54afebd3ad00ca11c2fe391`.

Method: official documentation and selected public source inspection. This comparison did not run competitor applications.
REA competitor binary analysis: NOT PERFORMED. Performance, GUI, accessibility and physical durability comparisons: NOT TESTED.
Main-branch source is not proof of an installed release's behavior. No competitor code or binary was adopted.

## Inspected primary licenses

| Project | Observed source pin | License evidence | WO28 treatment |
|---|---|---|---|
| Cryptomator desktop | `cb74d2c2adbca5c50b46ab60a5e1b4ac7165940a` | [GPLv3 text](https://github.com/cryptomator/cryptomator/blob/cb74d2c2adbca5c50b46ab60a5e1b4ac7165940a/LICENSE.txt); README also describes commercial licensing | Paraphrased design observations and source links only. No copied implementation. |
| BleachBit | `61f477afc2af11a982402294a319e741da4b97a0` | [COPYING](https://github.com/bleachbit/bleachbit/blob/61f477afc2af11a982402294a319e741da4b97a0/COPYING); README states GPLv3-or-later, including cleaners | No source or cleaner-rule import. |
| VeraCrypt | `21c4d8817bd746e320bfecead81d35978006fdc8` | [License](https://github.com/veracrypt/VeraCrypt/blob/21c4d8817bd746e320bfecead81d35978006fdc8/License.txt) combines Apache-2.0 and TrueCrypt-3.0 terms | No redistribution, driver or format implementation adoption. |
| PowerToys | `1b33221dfa19f30d71409eb550807883fb27283a` | [MIT](https://github.com/microsoft/PowerToys/blob/1b33221dfa19f30d71409eb550807883fb27283a/LICENSE) | Design reference only; no component copied. |
| SDelete / Sysinternals | Official documentation observed 2026-10-19 | [Microsoft terms](https://learn.microsoft.com/en-us/sysinternals/license-terms) | Documentation only. No binary reverse engineering or redistribution. |

The desktop application's license does not determine every separately published library's license. Inspect the exact dependency and version before adoption. Preserve required notices and source obligations for any future distribution.

## Evidence handling

Only original comparison prose, public URLs, source pins and PC-Care paths are committed by this research task. Third-party binaries, complete source files, screenshots and raw decompiler output are excluded. REA competitor-analysis status remains false for these five products.

## Reuse gate

1. Identify the exact component and artifact version.
2. Read its license and transitive notices.
3. Check compatibility with PC-Care's distribution policy.
4. Record required attribution and source delivery.
5. Test compatibility in an adapter.
6. Review security, performance and maintenance evidence.
7. Obtain required approval before production integration.

No new third-party dependency or cryptographic format is approved by this document.
