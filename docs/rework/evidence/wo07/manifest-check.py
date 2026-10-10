import sys,xml.etree.ElementTree as E
p,mode=sys.argv[1:]
versions={x.attrib.get('Include'):x.attrib.get('Version') for x in E.parse(p).iter('PackageVersion')}
expected=mode=='MODIFIED'
assert ('Microsoft.Diagnostics.Tracing.TraceEvent' in versions)==expected
assert ('Microsoft.Windows.CsWin32' in versions)==expected
if expected:
 assert versions['Microsoft.Diagnostics.Tracing.TraceEvent']=='3.2.8'
 assert versions['Microsoft.Windows.CsWin32']=='0.3.358'
print(mode+': TraceEvent '+('3.2.8' if expected else 'absent')+'; CsWin32 '+('0.3.358' if expected else 'absent'))
