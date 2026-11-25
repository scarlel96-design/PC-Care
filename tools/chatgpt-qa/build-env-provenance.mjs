/** Read-only build environment evidence. Do not infer tool versions absent from logs. */
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';

const KNOWN_SEVERITIES = new Set(['REVIEW', 'INCOMPLETE', 'MATCH']);
const semver = /^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$/;
const choose = values => [...new Set(values.filter(Boolean))];
const last = values => values.length ? values.at(-1) : null;
const capture = (source, regex) => choose([...source.matchAll(regex)].map(match => match[1]));
const bool = value => value === true ? true : value === false ? false : null;

function blockForDotnetAction(source) {
  const lines = source.split(/\r?\n/);
  const blocks = [];
  let block = null;
  for (const line of lines) {
    if (/^\s{6}-\s+(?:uses|name|run):/.test(line)) {
      if (block) blocks.push(block.join('\n'));
      block = [line];
    } else if (block && (/^\s{4}\S/.test(line) || /^\s{2}\S/.test(line) || /^\S/.test(line))) {
      blocks.push(block.join('\n')); block = null;
    } else if (block) block.push(line);
  }
  if (block) blocks.push(block.join('\n'));
  return blocks.filter(x => /^\s{6}-\s+uses:\s*['"]?actions\/setup-dotnet@/m.test(x));
}

export function parseWorkflow(name, source) {
  if (typeof name !== 'string' || typeof source !== 'string') throw new TypeError('Expected name and YAML text');
  const blocks = blockForDotnetAction(source);
  const selectors = [];
  const actions = [];
  for (const b of blocks) {
    const action = b.match(/^\s{6}-\s+uses:\s*['"]?(actions\/setup-dotnet@[^\s'"#]+)/m)?.[1];
    if (action) actions.push(action);
    for (const raw of capture(b, /^\s{10}dotnet-version:\s*['"]?([^'"#\r\n]+?)['"]?\s*(?:#.*)?$/gm)) {
      selectors.push(...raw.trim().split(/\s+/));
    }
  }
  const runsOn = capture(source, /^\s{4}runs-on:\s*['"]?([^'"#\r\n]+?)['"]?\s*(?:#.*)?$/gm);
  const pins = capture(source, /"sdk"\s*:\s*\{\s*"version"\s*:\s*"([0-9.]+)"/g);
  const rollForwardDisabled = /"rollForward"\s*:\s*"disable"/.test(source);
  const asserted = capture(source, /\bdotnet\s+--version\)?\s*-ne\s*['"](\d+\.\d+\.\d+)['"]/g);
  const exactSelectors = selectors.length > 0 && selectors.every(s => semver.test(s));
  const floatingSelectors = selectors.some(s => /(^|\.)(?:x|X|\*|latest)($|\.)|\$\{\{|\$\(/.test(s));
  return {
    name, runsOn, setupDotnetActions: actions, sdkSelectors: choose(selectors),
    sdkSelectorClass: !actions.length ? 'NOT_PRESENT' : !selectors.length ? 'UNKNOWN' : floatingSelectors ? 'FLOATING' : exactSelectors ? 'EXACT' : 'UNKNOWN',
    scriptedGlobalJsonPins: pins, rollForwardDisabled, sdkAssertions: asserted,
    performsDotnetBuild: /\bdotnet\s+(?:build|restore|test|publish)\b/.test(source),
  };
}

export function parseCentralPackages(source) {
  if (typeof source !== 'string') throw new TypeError('Expected XML text');
  const declared = Object.create(null);
  for (const match of source.matchAll(/<PackageVersion\b([^>]+)\/?\s*>/g)) {
    const attrs = match[1];
    const name = attrs.match(/\bInclude\s*=\s*"([^"]+)"/)?.[1];
    const version = attrs.match(/\bVersion\s*=\s*"([^"]+)"/)?.[1];
    if (name && version) {
      if (Object.hasOwn(declared, name)) throw new Error(`Repeated package declaration: ${name}`);
      declared[name] = version;
    }
  }
  return { centralManagementEnabled: /<ManagePackageVersionsCentrally>\s*true\s*<\/ManagePackageVersionsCentrally>/.test(source), declared: { ...declared },
    windowsAppSdkDeclared: declared['Microsoft.WindowsAppSDK'] ?? null,
    csWinRtDeclared: declared['Microsoft.Windows.CsWinRT'] ?? null,
    csWin32Declared: declared['Microsoft.Windows.CsWin32'] ?? null,
    caveat: 'Declared versions do not prove exact transitive restore resolution.' };
}

export function parseAppProject(source) {
  if (typeof source !== 'string') throw new TypeError('Expected project XML text');
  const tags = name => last(capture(source, new RegExp(`<${name}>\\s*([^<]+)\\s*</${name}>`, 'g')));
  const refs = capture(source, /<PackageReference\b[^>]*\bInclude\s*=\s*"([^"]+)"/g);
  return { targetFramework: tags('TargetFramework')?.trim() ?? null,
    useWinUI: /<UseWinUI>\s*true\s*<\/UseWinUI>/.test(source),
    packageReferences: refs,
    windowsAppSdkReferenced: refs.includes('Microsoft.WindowsAppSDK') };
}

export function parseJobLog(log) {
  if (typeof log !== 'string') throw new TypeError('Expected job log text');
  const clean = log.replace(/\x1b\[[0-9;]*m/g, '');
  const image = capture(clean, /^.*?Image Release:\s*(https?:\/\/[^\s\r\n]+)/gm);
  const selected = capture(clean, /^.*?dotnet --version result:\s*([0-9]+\.[0-9]+\.[0-9]+)\b/gm);
  const installed = capture(clean, /dotnet-install:\s*(?:Installed version is\s*|\.NET Core SDK with version\s*)['"]?(\d+\.\d+\.\d+)/g);
  const selector = capture(clean, /^\s*\S*\s+dotnet-version:\s*['"]?([^'"\s\r\n]+)/gm);
  const compiler = capture(clean, /\b(?:Microsoft \(R\) Visual C# Compiler version|Visual C# Compiler version|Roslyn compiler version):\s*([\w.+-]+)/g);
  const msbuild = capture(clean, /\bMSBuild version\s+([\w.+-]+)/g);
  const cswinrt = capture(clean, /\bCsWinRT(?: generator| analyzer)? version\s*[:=]\s*([\w.+-]+)/ig);
  const resolvedAppSdk = capture(clean, /\bRESOLVED_WINDOWS_APP_SDK_VERSION\s*[:=]\s*([\w.+-]+)/g);
  const errors = capture(clean, /\berror\s+([A-Za-z]+\d+)\s*:/g);
  return {
    source: 'job-log',
    runnerImageReleases: image,
    configuredSdkSelectors: selector,
    setupDotnetInstalledSdks: installed,
    explicitlyPrintedSelectedSdks: selected,
    compilerVersions: compiler,
    msbuildVersions: msbuild,
    csWinRtGeneratorVersions: cswinrt,
    resolvedWindowsAppSdkVersions: resolvedAppSdk,
    compilerErrorCodes: errors,
    confidence: { installedSdk: installed.length ? 'OBSERVED_SETUP' : 'UNKNOWN', selectedSdk: selected.length ? 'EXPLICIT_OUTPUT' : 'UNKNOWN',
      compilerVersion: compiler.length ? 'OBSERVED' : 'UNKNOWN', csWinRtGeneratorVersion: cswinrt.length ? 'OBSERVED' : 'UNKNOWN' },
    caveat: 'setup-dotnet installation is not proof of the SDK selected for each build. No missing version is inferred.',
  };
}

const FIELDS = [
  ['installedSdk', 'setup-dotnet SDK installation'],
  ['selectedSdk', 'executed dotnet SDK'],
  ['runnerImage', 'runner image release'],
  ['compilerVersion', 'C# compiler version'],
  ['msbuildVersion', 'MSBuild version'],
  ['windowsAppSdkDeclared', 'Windows App SDK declared version'],
  ['windowsAppSdkResolved', 'Windows App SDK resolved version'],
  ['csWinRtGeneratorVersion', 'CsWinRT generator version'],
  ['csWin32Declared', 'CsWin32 declared version'],
  ['buildCommand', 'build command'],
];

function field(value) { return typeof value === 'string' && value.trim() ? value.trim() : null; }

export function compareBuildEnvironments(left, right) {
  if (!left || !right || typeof left !== 'object' || typeof right !== 'object') throw new TypeError('Expected build observations');
  const differences = [], matches = [], unknown = [];
  for (const [key,label] of FIELDS) {
    const a = field(left[key]), b = field(right[key]);
    if (!a || !b) unknown.push({ key,label, left:a,right:b,reason:'Cannot compare an absent observation' });
    else if (a === b) matches.push({key,label,value:a});
    else differences.push({key,label,left:a,right:b});
  }
  const sameSourceCommit = bool(left.sourceSha && right.sourceSha ? left.sourceSha === right.sourceSha : null);
  const sameSourceTree = bool(left.treeSha && right.treeSha ? left.treeSha === right.treeSha : null);
  return {
    status: differences.length ? 'REVIEW' : unknown.length ? 'INCOMPLETE' : 'MATCH',
    leftRunId:left.runId ?? null, rightRunId:right.runId ?? null,
    differences,matches,unknown,sameSourceCommit,sameSourceTree,
    causeVerified:false,
    notes: [
      'Differences establish environmental drift, not causation.',
      'Identical source and clean A/B builds with logged toolchain versions are required for a causality claim.',
      'Unknown versions must not be filled with declared package versions.',
    ],
  };
}

export function auditCheckout(root, baseline = null) {
  const p = resolve(root);
  const folder = join(p,'.github','workflows');
  if (!existsSync(folder)) throw new Error('Missing .github/workflows');
  const workflows = readdirSync(folder).filter(n=>/\.ya?ml$/.test(n)).sort()
    .map(n=>parseWorkflow(n,readFileSync(join(folder,n),'utf8')));
  const packagesPath=join(p,'Directory.Packages.props');
  const csprojPath=join(p,'src','SmartPerformanceDoctor.App','SmartPerformanceDoctor.App.csproj');
  const packages=existsSync(packagesPath)?parseCentralPackages(readFileSync(packagesPath,'utf8')):null;
  const project=existsSync(csprojPath)?parseAppProject(readFileSync(csprojPath,'utf8')):null;
  const counts=workflows.reduce((acc,w)=>(acc[w.sdkSelectorClass]=(acc[w.sdkSelectorClass]??0)+1,acc),{});
  const regressions=[];
  if(baseline) {
    if (!baseline.workflows || typeof baseline.workflows!=='object') throw new TypeError('Invalid baseline');
    const byName=new Map(workflows.map(w=>[w.name,w]));
    for(const [name,entry] of Object.entries(baseline.workflows)) {
      const current=byName.get(name);
      if(!current) {regressions.push({file:name,reason:'Workflow removed'});continue;}
      if(entry.sdkSelectorClass!==current.sdkSelectorClass || JSON.stringify(entry.sdkSelectors??[])!==JSON.stringify(current.sdkSelectors))
        regressions.push({file:name,reason:'SDK selector drift',before:entry.sdkSelectors??[],after:current.sdkSelectors});
    }
    for(const current of workflows) if(!Object.hasOwn(baseline.workflows,current.name) && current.performsDotnetBuild)
      regressions.push({file:current.name,reason:'New .NET workflow requires review'});
    if(baseline.windowsAppSdkDeclared && packages?.windowsAppSdkDeclared !== baseline.windowsAppSdkDeclared)
      regressions.push({file:'Directory.Packages.props',reason:'Windows App SDK declaration drift',before:baseline.windowsAppSdkDeclared,after:packages?.windowsAppSdkDeclared??null});
  }
  return {status:regressions.length?'REVIEW':!packages||!project?'INCOMPLETE':'MATCH',workflows,counts,packages,project,regressions,
    limits:['Does not parse all YAML variants or MSBuild conditionals.','Transitive CsWinRT resolution requires obj/project.assets.json or a binlog.','Static inspection cannot establish the SDK chosen by the runner.']};
}

export function createBaseline(audit) {
  if (!audit || !Array.isArray(audit.workflows)) throw new TypeError('Expected checkout audit');
  return {schema:1,workflows:Object.fromEntries(audit.workflows.map(w=>[w.name,{sdkSelectorClass:w.sdkSelectorClass,sdkSelectors:w.sdkSelectors}])),
    windowsAppSdkDeclared:audit.packages?.windowsAppSdkDeclared??null};
}

export { KNOWN_SEVERITIES };
