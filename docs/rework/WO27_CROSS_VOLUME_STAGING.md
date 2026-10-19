# WO27 - Cross Volume Staging

Production keeps PUBLISH-SAME-VOLUME-REQUIRED before installation mutation. Experiment A stages on the target volume. Experiment B copies into a protected target-volume directory. Both verify length, hash, bound identity and ACL before same-volume publication.

The prototype runs only on a disposable GitHub-hosted runner. It rejects publication collisions and preserves collision residue. Copy interruption, guest privileges and media durability remain unqualified. Neither experiment connects to production. Cross-volume Rename is not atomic publication.

## Final automated qualification

Source: `f6d29413a77c7522210de6eba3b1dbae745b2365`.

CI: [39197282336 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39197282336).

App: 1445 PASS / 0 FAIL / 111 SKIP. New WO27: 16 PASS, included.

SecurityLab: 36 PASS, separate. Restore and Release: 0 warnings / 0 errors.

Actual guest deployment, privileged recovery, UAC, reboot, GUI and physical durability remain NOT TESTED.
