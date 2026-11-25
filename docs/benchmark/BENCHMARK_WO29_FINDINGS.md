# WO-29 comparative review

[Cryptomator](https://docs.cryptomator.org/desktop/password-and-recovery-key/): Recovery credentials need a separate handling policy. Test recovery on a copy. Verify every exported file before reporting success.

[BleachBit](https://docs.bleachbit.org/cml/cleanerml/): Cleaner rules separate selection and action. Rules alone cannot authorize deletion. Compare preview inventory with a synthetic tree. Keep production cleanup disabled.

[SDelete](https://learn.microsoft.com/en-us/sysinternals/downloads/sdelete): Free-space cleaning has limits, including filename remnants. Label the selected scope and medium. Never infer physical SSD erasure from file disappearance.

[VeraCrypt](https://veracrypt.io/en/Program%20Menu.html): Header restoration addresses volume access; file integrity needs separate evidence. Reopen a fixture copy and verify authenticated extraction after recovery.

[PowerToys](https://raw.githubusercontent.com/microsoft/PowerToys/main/src/modules/interface/powertoy_module_interface.h): The public module interface separates settings from enable and disable operations. Test repeated start, cancel and dispose. Verify that the module releases resources.
