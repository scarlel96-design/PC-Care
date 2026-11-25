import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {scanCSharp} from '../../tools/chatgpt-qa/cswinrt-static.mjs';
const fixtures=JSON.parse(readFileSync(fileURLToPath(new URL('./fixtures/cswinrt-known-patterns.json',import.meta.url)),'utf8'));
for(const entry of fixtures.examples) {
 test(`known source-shaped ${entry.sourcePath} detects ${entry.expectedRule}`,()=>{
   const out=scanCSharp(entry.snippet,entry.sourcePath);
   assert.ok(out.findings.some(f=>f.rule===entry.expectedRule && f.confidence==='HIGH'),JSON.stringify(out));
 });
}
