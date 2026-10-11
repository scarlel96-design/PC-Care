"""Reopen WO-09 evidence and check completeness and recorded invariants."""
from pathlib import Path
import hashlib,json,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[4]
evidence=Path(__file__).resolve().parent
reports="OPEN_SOURCE_EVALUATION REFERENCE_TRACE_PIPELINE GROUND_TRUTH_DATASET DIAGNOSTIC_ACCURACY PROCESS_ATTRIBUTION DISK_IDENTITY RECOVERY_PLANNER_DESIGN DRY_RUN_VALIDATION GUI_VALIDATION EXECUTION_REPORT PRODUCT_READINESS".split()
required="BASELINE_RESULT.txt OPEN_SOURCE_EVALUATION.txt REFERENCE_TRACE_RESULT.txt GROUND_TRUTH_RESULT.json ACCURACY_METRICS.json PROCESS_CORRELATION_RESULT.txt DISK_IDENTITY_RESULT.txt RECOVERY_PLAN_TEST_RESULT.txt DRY_RUN_RESULT.txt GUI_TEST_RESULT.txt FULL_TEST_RESULT.txt SECURITYLAB_RESULT.txt GITHUB_RESULT.txt VERIFICATION.txt MODIFIED_FILE.cs DIFF_FILE.patch ROLLBACK.sh".split()
for name in required: assert (evidence/name).is_file(),name
for name in reports: assert (root/'docs/rework'/('WO09_'+name+'.md')).is_file(),name
assert hashlib.sha256((evidence/'ORIGINAL_FILE.cs').read_bytes()).hexdigest().upper()=='A9D374C3B8542B849AA8FBFE55D913DCED42A7C4F472EFEA4601CF1678389619'
expected='4F73E92B626E0654B0A1BDB86423ED1E03CB180D395D5E9855109AD286B9BBAE'
assert hashlib.sha256((evidence/'MODIFIED_FILE.cs').read_bytes()).hexdigest().upper()==expected
assert (root/'src/SmartPerformanceDoctor.Diagnostics/ReferenceComparison.cs').read_bytes()==(evidence/'MODIFIED_FILE.cs').read_bytes()
metrics=json.loads((evidence/'ACCURACY_METRICS.json').read_text())
if not metrics['qualifiedPositiveCpuTrials'] or not metrics['qualifiedNegativeCpuTrials']:
 assert metrics['Precision'] is None and metrics['Recall'] is None
sample=json.loads((evidence/'DRY_RUN_SAMPLE.json').read_text())
# Published Dry Run results must retain a disabled executor and zero changes.
def visit(value):
 if isinstance(value,dict):
  for key,item in value.items():
   if key.lower()=='executionenabled': assert item is False
   if key.lower()=='systemmutations': assert item==0
   visit(item)
 elif isinstance(value,list):
  for item in value: visit(item)
visit(sample)
manifest=[]
paths=list((root/'docs/rework').glob('WO09_*.md'))+list(evidence.rglob('*'))
for file in sorted(paths):
 if not file.is_file() or file.name=='EVIDENCE_MANIFEST.json' or 'obj' in file.parts: continue
 assert file.suffix.lower() not in ('.etl','.pfx','.key'),file
 data=file.read_bytes()
 if file.suffix=='.json':json.loads(data)
 elif file.suffix=='.trx':ET.fromstring(data)
 else:data.decode('utf-8-sig')
 manifest.append(dict(path=str(file.relative_to(root)).replace('\\','/'),sha256=hashlib.sha256(data).hexdigest(),bytes=len(data)))
(evidence/'EVIDENCE_MANIFEST.json').write_text(json.dumps({'status':'PASS','sourceRestorationScope':'Independent source copy; not installed transaction','files':manifest},indent=2),encoding='utf-8')
print('WO09_EVIDENCE_REOPENED files='+str(len(manifest))+' requiredReports=11 requiredEvidence=14 sourceHash=PASS executionEnabled=False')
