"""Import anonymous CI measurements and redact runner identifiers in TRX copies."""
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

source = Path(sys.argv[1])
destination = Path(sys.argv[2])
destination.mkdir(parents=True, exist_ok=True)
namespace = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
test_names = {
    "wo06-full.trx": "CI_FULL.trx",
    "wo09-performance.trx": "CI_PERFORMANCE.trx",
    "wo06-signed-updater.trx": "CI_UPDATER.trx",
    "wo06-securitylab.trx": "CI_SECURITYLAB.trx",
}
counts = {}
redactions = []
for original_name, published_name in test_names.items():
    matches = list(source.rglob(original_name))
    assert len(matches) == 1, matches
    raw = matches[0].read_bytes()
    tree = ET.fromstring(raw)
    counter = tree.find("t:ResultSummary/t:Counters", namespace)
    assert counter is not None
    counts[published_name] = dict(counter.attrib)
    outcomes = [item.get("outcome") for item in tree.findall("t:Results/t:UnitTestResult", namespace)]
    counts[published_name]["observedSkippedResults"] = sum(outcome == "NotExecuted" for outcome in outcomes)
    counts[published_name]["observedPassedResults"] = sum(outcome == "Passed" for outcome in outcomes)
    counts[published_name]["observedFailedResults"] = sum(outcome == "Failed" for outcome in outcomes)
    text = raw.decode("utf-8-sig")
    hosts = set(re.findall(r'computerName="([^"]+)"', text))
    for host in hosts:
        text = text.replace(host, "[CI_HOST]")
    text = re.sub(r"D:\\a\\PC-Care\\PC-Care", "[CI_WORKSPACE]", text, flags=re.I)
    text = re.sub(r"C:\\Users\\[^\\<\s]+", "[CI_USER_DIRECTORY]", text, flags=re.I)
    public = text.replace("\r\n", "\n").encode("utf-8")
    ET.fromstring(public)
    (destination / published_name).write_bytes(public)
    redactions.append({"file": published_name, "originalSha256": hashlib.sha256(raw).hexdigest(),
                       "publishedSha256": hashlib.sha256(public).hexdigest(),
                       "transformation": "Mask runner host, workspace, and user directory. Normalize LF. Preserve test outcomes."})

workloads = []
for measurement in sorted(source.glob("artifacts/wo08-ci/*/measurement.json")):
    data = json.loads(measurement.read_text(encoding="utf-8-sig"))
    workers = list(measurement.parent.glob("workload-*.json"))
    assert len(workers) == 1, workers
    worker = json.loads(workers[0].read_text(encoding="utf-8-sig"))
    workloads.append({"mode": measurement.parent.name, "workload": worker,
                      "WorkloadExecuted": True, "PressureVerified": "Unknown",
                      "independentTruth": "NOT TESTED", "environment": data.get("environment"),
                      "collectorOverheadPercentOfMachine": data.get("overheadPercentOfMachine"),
                      "collectorPeakWorkingSetBytes": data["snapshot"]["PeakWorkingSetBytes"],
                      "etwStatistics": data["snapshot"]["EtwStatistics"]})
assert len(workloads) == 8, len(workloads)
(destination / "CI_WORKLOAD_SMOKE.json").write_text(json.dumps(workloads, indent=2), encoding="utf-8")
(destination / "CI_TEST_COUNTS.json").write_text(json.dumps(counts, indent=2), encoding="utf-8")
(destination / "CI_EVIDENCE_REDACTION.json").write_text(json.dumps(redactions, indent=2), encoding="utf-8")
process_results = list(source.rglob("process-results.json"))
assert len(process_results) == 1
process_data = json.loads(process_results[0].read_text(encoding="utf-8-sig"))
(destination / "CI_PROCESS_RECOVERY.json").write_text(json.dumps(process_data, indent=2), encoding="utf-8")
expected_matches = sum((item["ExitCode"] == 0) == (item["Expected"] == "ZERO") for item in process_data)
assert expected_matches == len(process_data)
print(json.dumps({"testCounts": counts, "workloadSmokeCount": len(workloads),
                  "recoveryCommands": len(process_data), "expectedRecoveryResults": expected_matches}, indent=2))
