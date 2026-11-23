import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { resolve, join } from 'node:path';
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import assert from 'node:assert/strict';

const [packageDirectory, outputDirectory] = process.argv.slice(2);
assert(packageDirectory && outputDirectory, 'Pass the pinned REA package and private output directories.');
const pkg = resolve(packageDirectory), out = resolve(outputDirectory);
assert.equal(JSON.parse(readFileSync(join(pkg, 'package.json'))).version, '6.7.0');
mkdirSync(out, { recursive: true });
const root = resolve('tools/BenchmarkSamples');
const launcher = join(pkg, 'scripts/rea.mjs');
const digest = value => createHash('sha256').update(value).digest('hex');
const save = (name, value) => writeFileSync(join(out, name + '.json'), JSON.stringify(value, null, 2));
const records = [];
function evidence(text) {
  try { const value = JSON.parse(text); if (value.evidence_id) return value; } catch {}
  for (const line of text.split(/\r?\n/).reverse()) {
    try { const value = JSON.parse(line); if (value.evidence_id) return value; } catch {}
  }
  throw new Error('REA_EVIDENCE_MISSING');
}
function cli(name, args) {
  const start = performance.now();
  const result = spawnSync(process.execPath, [launcher, ...args, '--json'], { encoding: 'utf8', timeout: 120000, maxBuffer: 32 * 1024 * 1024, windowsHide: true });
  writeFileSync(join(out, name + '.stdout.txt'), result.stdout ?? '');
  writeFileSync(join(out, name + '.stderr.txt'), result.stderr ?? '');
  records.push({ Command: [process.execPath, launcher, ...args, '--json'], ExitCode: result.status, Signal: result.signal, Error: result.error?.message ?? null, ElapsedMs: performance.now() - start });
  assert.equal(result.status, 0, name + ': CLI failed');
  const value = evidence(result.stdout);
  save(name, value);
  return value;
}
const plainPath = join(root, 'Plain/bin/Release/net10.0/Plain.dll');
const callsPath = join(root, 'Calls/bin/Release/net10.0/Calls.dll');
const plain = cli('plain-artifact', ['inspect-managed-artifact', plainPath]);
const plainMembers = cli('plain-members', ['inspect-managed-members', plainPath]);
const calls = cli('calls-artifact', ['inspect-managed-artifact', callsPath]);
const members = cli('calls-members', ['inspect-managed-members', callsPath]);
for (const value of [plain, calls]) assert.equal(value.normalized_result.classification.status, 'managed');
assert(plainMembers.normalized_result.methods.some(x => x.name === 'Version'));
const m = members.normalized_result;
assert(m.types.some(x => x.full_name === 'PcCare.BenchmarkSamples.Calculator'));
assert(m.methods.some(x => x.name === 'Double' && x.body.instruction_count > 0));
assert(m.call_edges.some(x => x.caller === 'PcCare.BenchmarkSamples.Calculator.Evaluate' && x.target_name === 'Double' && x.opcode === 'call'));
const js = cli('javascript', ['analyze-javascript-application', join(root, 'JavaScript')]);
const graph = js.normalized_result.graph;
for (const path of ['index.js', 'service.js', 'math.js'])
  assert(graph.nodes.some(x => x.observations.some(o => o.properties?.path === path)), 'Missing JavaScript module: ' + path);
for (const path of ['service.js', 'math.js'])
  assert(graph.edges.some(x => x.relation === 'imports' && x.properties?.resolved_path === path && x.properties?.resolution_status === 'resolved'));
assert(graph.edges.some(x => x.relation === 'loads' && x.properties?.declared_path === 'index.js'));
const semanticUnknowns = js.normalized_result.semantic_graph.unknowns;
const req = createRequire(join(pkg, 'package.json'));
const { Client } = await import(pathToFileURL(req.resolve('@modelcontextprotocol/client')).href);
const { StdioClientTransport } = await import(pathToFileURL(req.resolve('@modelcontextprotocol/client/stdio')).href);
const client = new Client({ name: 'pccare-wo28-qualification', version: '1.0.0' });
const transport = new StdioClientTransport({ command: process.execPath, args: [launcher, 'mcp'], stderr: 'pipe' });
let ownedPid = null;
const smoke = { ActiveCodexSessionConnected: false, Scope: 'Independent SDK stdio client; not the running Codex tool connection' };
try {
  await client.connect(transport);
  ownedPid = transport.pid;
  smoke.Server = client.getServerVersion();
  const list = await client.listTools();
  smoke.ToolCount = list.tools.length;
  const names = ['binary_session', 'inspect_managed_members', 'analyze_javascript_application'];
  smoke.Schemas = list.tools.filter(x => names.includes(x.name));
  assert.equal(smoke.Schemas.length, names.length);
  const session = await client.callTool({ name: 'binary_session', arguments: {} });
  const result = await client.callTool({ name: 'inspect_managed_members', arguments: { path: callsPath } });
  assert(!result.isError);
  save('mcp-session', session); save('mcp-managed', result);
  const value = result.structuredContent?.evidence_id ? result.structuredContent : evidence(result.content.filter(x => x.type === 'text').map(x => x.text).join('\n'));
  assert(value.normalized_result.call_edges.some(x => x.target_name === 'Double'));
  smoke.AnalysisEvidenceId = value.evidence_id;
  smoke.TransportAnalysisPass = true;
} finally {
  await client.close();
  if (ownedPid) {
    let alive = true;
    for (let i = 0; i < 20; i++) {
      try { process.kill(ownedPid, 0); } catch { alive = false; break; }
      await new Promise(r => setTimeout(r, 50));
    }
    smoke.OwnedServerExited = !alive;
    // A recycled numeric PID is not proof of process ownership.
  }
  save('mcp-smoke', smoke);
}
assert(smoke.OwnedServerExited, 'MCP cleanup did not complete');
save('cli-commands', records);
save('sample-result', { Status: 'PASS', ReaVersion: '6.7.0', ManagedArtifacts: 2, JavaScriptApplications: 1, StaticAnalysisOnly: true, ManagedCallsVerified: true, JavaScriptImportsVerified: true, JavaScriptGeneralCallsQualified: false, JavaScriptSemanticUnknowns: semanticUnknowns.length, JavaScriptEvidenceId: js.evidence_id, ArtifactHashes: { Plain: digest(readFileSync(plainPath)), Calls: digest(readFileSync(callsPath)) }, MCP: smoke });
console.log('WO28_REA_SAMPLE_VALIDATION_PASS');
