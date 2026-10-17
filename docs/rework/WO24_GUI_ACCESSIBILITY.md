# WO24 — GUI and accessibility

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Reuse GuiProbe and existing WinUI automation identifiers.
Build the GUI tool and retain the existing source contract checks.
No independent interactive test profile was supplied.
Actual application navigation, UAC screens, DPI and screen reader remain NOT TESTED.
No screen-control session was started or reconnected.
End any future screen-control session immediately after its approved test.

## Approved interactive procedure

1. Confirm the dedicated guest SID and application data boundary.
2. Start the source-bound product in that guest profile.
3. Record the product process ID and visible window.
4. Navigate each required product page.
5. Run the matching GuiProbe verb against that observed process ID.
6. Record actual clicks, focus movement and resulting status text.
7. Check high DPI, narrow layout and reduced motion separately.
8. Test screen-reader output with an actual screen reader.
9. Close the test application and end any screen-control session.

Existing GuiProbe verbs cover diagnostics, StorageCare, vault inspection and update-window inspection.
The tool returns PARTIAL for these bounded interactions.
It does not prove all requested pages or accessibility behaviors.
No standalone GUI tool build establishes product GUI execution.
The current work did not start or reconnect a screen-control session.
