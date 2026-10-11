import json,sys,statistics,shutil
from pathlib import Path
source=Path(sys.argv[1]); dest=Path(sys.argv[2]);dest.mkdir(parents=True,exist_ok=True)
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
files=list(source.rglob('reference-execution.json'))
assert len(files)==1,files
execution=read(files[0]);root=files[0].parent
trials=[]; measurements=[]
for item in execution:
 n=item['trial'];p=root/f'reference-{n}.json';report=read(p);m=read(root/f'measurement-{n}.json');measurements.append(m)
 q=report.get('qualification',{});ref=report.get('reference',{});state=q.get('PressureState','Unknown');metrics=q.get('Metrics',{})
 qualified=state in ('PressureVerified','NoSustainedCpuPressureObserved') and ref.get('LostEvents')==0 and ref.get('LostBuffers')==0 and ref.get('CpuGaps')==0
 error=q.get('CpuMedianAbsoluteError');counts={k:metrics.get(k,0) for k in ['TruePositive','FalsePositive','FalseNegative','TrueNegative','Unknown','InsufficientEvidence','ReferenceMismatch']}
 trial=dict(trial=n,mode=item['mode'],WorkloadExecuted=item.get('WorkloadExecuted',False),PressureVerified=state=='PressureVerified',pressureState=state,qualifiedCpu=qualified,
   referenceLoss={k:ref.get(k) for k in ['LostEvents','LostBuffers','CpuGaps']},alignedWindow=q.get('Window'),exclusions=q.get('Exclusions',[]),counts=counts,
   cpuMedianAbsoluteErrorPercentagePoints=error,predeclaredErrorTargetMet=error is not None and error<=10,
   referenceCpuMedian=statistics.median([p['CpuPercent'] for p in ref.get('Cpu',[]) if p.get('CpuPercent') is not None]) if any(p.get('CpuPercent') is not None for p in ref.get('Cpu',[])) else None,
   collectorOverheadPercentOfMachine=m.get('overheadPercentOfMachine'),collectorPeakWorkingSetBytes=m['snapshot']['PeakWorkingSetBytes'],analyzerResources=report.get('resource'),
   execution=item, environment=m.get('environment'), appEtwStatistics=m['snapshot']['EtwStatistics'],
   referenceParserDiagnostics=report.get('cpuParserDiagnostics'), independentLifetimeCount=len(report.get('lifetimes',[])), unlinkedCpu=report.get('unlinkedCpu'), unlinkedIo=report.get('unlinkedIo'))
 trials.append(trial)
 shutil.copyfile(p,dest/f'CI_REFERENCE_{n}.json')
qtr=[t for t in trials if t['qualifiedCpu']];positive=sum(t['PressureVerified'] for t in qtr);negative=len(qtr)-positive
counts={k:sum(t['counts'][k] for t in trials) for k in ['TruePositive','FalsePositive','FalseNegative','TrueNegative','Unknown','InsufficientEvidence','ReferenceMismatch']}
balanced=positive>0 and negative>0
precision=counts['TruePositive']/(counts['TruePositive']+counts['FalsePositive']) if balanced and counts['TruePositive']+counts['FalsePositive'] else None
recall=counts['TruePositive']/(counts['TruePositive']+counts['FalseNegative']) if balanced and counts['TruePositive']+counts['FalseNegative'] else None
complete=len(qtr)==6 and balanced
accuracy_pass=complete and all(t['predeclaredErrorTargetMet'] for t in qtr) and counts['FalsePositive']==0 and counts['FalseNegative']==0
status='PASS' if accuracy_pass else 'FAIL' if complete else 'PARTIAL'
metrics={'status':'PARTIAL','cpuQualificationStatus':status,'scope':'Six controlled CPU trials on one isolated GitHub Windows runner; not general product accuracy',
 'trialCount':len(trials),'qualifiedCpuTrials':len(qtr),'qualifiedPositiveCpuTrials':positive,'qualifiedNegativeCpuTrials':negative,
 'counts':counts,'Precision':precision,'Recall':recall,'processContributionError':None,'diskMeasurementError':None,
 'memoryPressureAccuracy':'NOT TESTED','diskPressureAccuracy':'NOT TESTED','actualPidReuse':'NOT TESTED','gui':'NOT TESTED',
 'predeclaredTargets':{'minimumCommonWindowSeconds':10,'maximumCpuMedianAbsoluteErrorPercentagePoints':10,'zeroFalseClassifications':True,'bothPositiveAndNegativeRequired':True},
 'trials':[{k:v for k,v in t.items() if k!='execution'} for t in trials]}
(dest/'ACCURACY_METRICS.json').write_text(json.dumps(metrics,indent=2),encoding='utf-8')
(dest/'GROUND_TRUTH_RESULT.json').write_text(json.dumps({'status':'PARTIAL','conditions':'Three sustained CPU trials followed by three short-peak trials; 30-second bounded collection; first two workload seconds excluded','trials':trials,'memoryAndDiskTruth':'Unknown'},indent=2),encoding='utf-8')
shutil.copyfile(files[0],dest/'CI_REFERENCE_EXECUTION.json')
summary={'cpuStatus':status,'qualified':len(qtr),'positive':positive,'negative':negative,'counts':counts,'Precision':precision,'Recall':recall,'cpuErrors':[t['cpuMedianAbsoluteErrorPercentagePoints'] for t in trials],
 'nativeEtwLost':[m['snapshot']['EtwStatistics']['NativeEtwEventsLost'] for m in measurements],
 'queueDropped':[m['snapshot']['EtwStatistics']['ApplicationQueueDropped'] for m in measurements],
 'cleanup':[m['snapshot']['EtwStatistics']['SessionCleanupStatus'] for m in measurements],
 'collectorCPU':[m.get('overheadPercentOfMachine') for m in measurements],
 'peakMemoryMiB':[m['snapshot']['PeakWorkingSetBytes']/1048576 for m in measurements],
 'referenceGaps':[t['referenceLoss']['CpuGaps'] for t in trials],
 'parserDiagnostics':[t['referenceParserDiagnostics'] for t in trials]}
print(json.dumps(summary,indent=2))
