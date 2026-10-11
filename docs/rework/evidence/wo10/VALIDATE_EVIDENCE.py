"""Validate recorded evidence. This command does not execute product tests."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET

evidence = Path(__file__).resolve().parent
repository = evidence.parents[3]
source = "c23802b2adc7be63c8c15ab6cd313ec9be0a05db"

def read_json(name):
    return json.loads((evidence / name).read_text(encoding="utf-8-sig"))

def git(*args):
    return subprocess.check_output(["git", *args], cwd=repository, text=True).strip()

reports = ["OPEN_SOURCE_EVALUATION", "CPU_REFERENCE_SEMANTICS", "CPU_GAP_ANALYSIS",
           "INDEPENDENT_ACCURACY", "RECOVERY_EXECUTION_DESIGN", "SANDBOX_EXECUTION",
           "ROLLBACK_VALIDATION", "GUI_AUTOMATION", "PERFORMANCE_BUDGET",
           "EXECUTION_REPORT", "PRODUCT_READINESS"]
required = ["BASELINE_RESULT.txt", "OPEN_SOURCE_EVALUATION.txt", "CPU_REFERENCE_RESULT.txt",
            "CPU_GAP_DIAGNOSIS.json", "ACCURACY_METRICS.json", "IDLE_REFERENCE_COMPARISON.json",
            "RECOVERY_PLAN_RESULT.txt", "SANDBOX_EXECUTION_RESULT.txt", "ROLLBACK_RESULT.txt",
            "GUI_AUTOMATION_RESULT.txt", "PERFORMANCE_BUDGET_RESULT.txt", "FULL_TEST_RESULT.txt",
            "SECURITYLAB_RESULT.txt", "GITHUB_RESULT.txt", "VERIFICATION.txt",
            "MODIFIED_FILE.cs", "DIFF_FILE.patch", "ROLLBACK.sh"]
for report in reports:
    assert (repository / "docs/rework" / f"WO10_{report}.md").read_text(encoding="utf-8-sig")
for name in required:
    assert (evidence / name).read_bytes(), name

assert git("rev-parse", f"{source}^{{tree}}") == "752a647f89d6cced6637c1224e5e2dcae9618776"
for directory in ["src", "tests", "tools", "scripts", ".github"]:
    assert not git("diff", "--name-only", source, "--", directory), directory
for file in read_json("PROTECTED_SOURCE_INVENTORY.json")["files"]:
    assert git("rev-parse", f"HEAD:{file['path']}") == file["baselineBlob"], file["path"]

for name, expected in [("wo06-full.trx", {"Passed": 957, "NotExecuted": 111}),
                       ("wo06-signed-updater.trx", {"Passed": 128}),
                       ("wo09-performance.trx", {"Passed": 188}),
                       ("wo06-securitylab.trx", {"Passed": 36})]:
    xml = ET.fromstring((evidence / "ci" / name).read_bytes())
    assert dict(Counter(n.attrib["outcome"] for n in xml.findall(".//{*}UnitTestResult"))) == expected

gap = read_json("CPU_GAP_DIAGNOSIS.json")
assert len(gap["trials"]) == 12
assert gap["interpretation"]["baselineNarrativeConflict"] is True
for trial in gap["trials"]:
    assert trial["QualifiedReference"] is False
    assert trial["nativeEtwEventsLost"] == trial["nativeEtwBuffersLost"] == 0
    assert all(code == 0 for code in trial["exitCodes"].values())
accuracy = read_json("ACCURACY_METRICS.json")
assert accuracy["qualifiedTrials"] == 0 and accuracy["Unknown"] == 12
assert accuracy["Precision"] is None and accuracy["Recall"] is None
ci = read_json("CI_STATUS.json")
assert ci["conclusion"] == "success" and ci["headSha"] == source
crashes = [t for t in read_json("ci/SANDBOX_RESULT.json") if "beforeSha256" in t]
assert len(crashes) == 6
for trial in crashes:
    assert trial["beforeSha256"] == trial["afterSha256"] and trial["exitCode"] == 0
    assert json.loads(trial["output"])["BytesVerified"] is True
assert read_json("ci/GUI_STATUS.json")["guiExecuted"] is False

for name, digest in [("ORIGINAL_FILE.cs", "4F73E92B626E0654B0A1BDB86423ED1E03CB180D395D5E9855109AD286B9BBAE"),
                     ("MODIFIED_FILE.cs", "26688B129E9AF8839954929CD2AE4B2725F7DD9CFAE1E6083A977680D4A78F72")]:
    assert hashlib.sha256((evidence / name).read_bytes()).hexdigest().upper() == digest

manifest = evidence / "EVIDENCE_MANIFEST.sha256"
if manifest.exists():
    for row in manifest.read_text(encoding="utf-8").splitlines():
        digest, name = row.split("  ", 1)
        assert hashlib.sha256((repository / name).read_bytes()).hexdigest() == digest, name
assert not [p for p in evidence.rglob("*") if p.suffix.lower() in (".etl", ".pfx") or p.name == "key.bin"]
print("EVIDENCE_PASS reports=11 requiredEvidence=15 app=957/111/0 performance=188 updater=128 securityLab=36 qualifiedCpu=0/12 crashRestore=6/6 gui=NOT_TESTED")
