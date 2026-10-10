from pathlib import Path
import hashlib,json,xml.etree.ElementTree as E,subprocess
r=Path.cwd(); d=r/'docs/rework'; e=d/'evidence/wo06'
reports=['EXECUTION_REPORT','BOOTSTRAP_DESIGN','SELF_UPDATE_VALIDATION','LEGACY_JOURNAL_MIGRATION','JOURNAL_DURABILITY','NEW_FILE_RECOVERY','AUTHENTICODE_IDENTITY','VM_E2E_REPORT','RELEASE_READINESS']
required=['VERIFICATION','BUILD_RESULT','TEST_RESULT','SELF_UPDATE_RESULT','JOURNAL_MIGRATION_RESULT','CRASH_RECOVERY_RESULT','AUTHENTICODE_RESULT','VM_E2E_RESULT','GITHUB_RESULT']
for p in [*(d/f'WO06_{x}.md' for x in reports),*(e/f'{x}.txt' for x in required),e/'UpdateTransaction.original.cs',e/'UpdateTransaction.modified.cs',e/'DIFF_FILE.patch',e/'ROLLBACK.sh',e/'SourceRollback.ps1']:
    b=p.read_bytes(); assert b, p
    if p.suffix not in ['.cs','.patch']: b.decode('utf-8-sig')
orig=hashlib.sha256((e/'UpdateTransaction.original.cs').read_bytes()).hexdigest().upper()
assert orig=='63A4AD13AB5A4B105C4B438DD4DC25E3F1449AE918EBA005946103DC77871C94'
original=(e/'UpdateTransaction.original.cs').read_bytes()
baseline=(r/'artifacts/wo06-baseline/src/SmartPerformanceDoctor.UpdateEngine/UpdateTransaction.cs').read_bytes()
blob=subprocess.check_output(['git','show','291f84daf1e950ba9cb74af0c8fea8879a11174e:src/SmartPerformanceDoctor.UpdateEngine/UpdateTransaction.cs'])
assert original.replace(b'\r\n',b'\n')==baseline.replace(b'\r\n',b'\n')==blob
assert (e/'UpdateTransaction.modified.cs').read_bytes()==(r/'src/SmartPerformanceDoctor.UpdateEngine/UpdateTransaction.cs').read_bytes()
assert (r/'artifacts/wo06-source-rollback/UpdateTransaction.cs').read_bytes()==(e/'UpdateTransaction.original.cs').read_bytes()
assert (e/'DIFF_FILE.patch').read_bytes()==subprocess.check_output(['git','diff','--binary','291f84daf1e950ba9cb74af0c8fea8879a11174e','5647b352cf41082def74bef2701b6fe707c51cc0'])
for name,total,passed,notex in [('BASELINE',91,91,0),('UPDATER',128,128,0),('FULL',880,769,111),('SECURITYLAB',36,36,0)]:
    trx=E.parse(e/f'{name}.trx')
    c=trx.find('.//{*}Counters').attrib
    results=trx.findall('.//{*}UnitTestResult')
    skipped=sum(x.attrib['outcome']=='NotExecuted' for x in results)
    assert (int(c['total']),int(c['passed']),skipped,int(c['failed']))==(total,passed,notex,0),(name,c)
    assert len(results)==total
commands=json.loads((e/'PROCESS_COMMANDS.json').read_text(encoding='utf-8-sig'))
assert len(commands)==121
assert sum(c['ExitCode']==0 for c in commands)==81
assert sum(c['ExitCode']==-1 for c in commands)==37
assert sum(c['ExitCode']==5 for c in commands)==3
for c in commands:
    if any(v in c['Command'] for v in [' kill:',' apply-kill ',' recover-kill',' recover-final-kill']): assert c['ExitCode']==-1,c
assert json.loads((e/'vault-source-before.json').read_text(encoding='utf-8-sig'))==json.loads((e/'vault-source-after.json').read_text(encoding='utf-8-sig'))
assert b'\r' not in (e/'ROLLBACK.sh').read_bytes()
print('EVIDENCE_REOPEN=PASS; REPORTS=9; REQUIRED_RECEIPTS=9; LOCAL_TRX=128/880/36; PROCESS_COMMANDS=121; SOURCE_COPY_ROLLBACK=PASS')
