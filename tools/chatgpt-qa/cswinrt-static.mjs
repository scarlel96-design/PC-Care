/** Read-only CsWinRT1032 risk scanner. Results do not replace compilation. */

export const NON_CONCRETE_INTERFACES = Object.freeze([
  'IEnumerable', 'IReadOnlyList', 'IReadOnlyCollection', 'ICollection', 'IList',
]);

function space(ch) { return ch === '\n' || ch === '\r' ? ch : ' '; }

// Ignore strings and comments while retaining source offsets and line breaks.
export function maskCSharpText(source) {
  if (typeof source !== 'string') throw new TypeError('Expected C# source text');
  const buf = source.split('');
  const write = (start, end) => { for (let k = start; k < end; k++) buf[k] = space(source[k]); };
  let i = 0;
  while (i < source.length) {
    if (source.startsWith('//', i)) {
      const start = i; i += 2;
      while (i < source.length && source[i] !== '\n') i++;
      write(start, i); continue;
    }
    if (source.startsWith('/*', i)) {
      const start = i; const end = source.indexOf('*/', i + 2);
      i = end === -1 ? source.length : end + 2;
      write(start, i); continue;
    }
    if (source[i] === '\'' || source[i] === '"' || source[i] === '@' || source[i] === '$') {
      const start = i;
      let quote = '', verbatim = false, rawCount = 0;
      if (source[i] === '\'') quote = '\'';
      else {
        let j = i;
        if (source[j] === '@' || source[j] === '$') {
          const a = source[j++];
          if ((source[j] === '@' || source[j] === '$') && source[j] !== a) j++;
        }
        if (source[j] === '"') {
          quote = '"'; verbatim = source.slice(i, j).includes('@');
          while (source[j + rawCount] === '"') rawCount++;
          i = j;
        }
      }
      if (!quote) { i++; continue; }
      if (rawCount >= 3) {
        i += rawCount;
        const ending = '"'.repeat(rawCount); const end = source.indexOf(ending, i);
        i = end === -1 ? source.length : end + rawCount;
      } else {
        i++;
        while (i < source.length) {
          if (!verbatim && source[i] === '\\') { i += 2; continue; }
          if (source[i] === quote) {
            if (verbatim && quote === '"' && source[i+1] === '"') { i += 2; continue; }
            i++; break;
          }
          i++;
        }
      }
      write(start, Math.min(i, source.length)); continue;
    }
    i++;
  }
  return buf.join('');
}

function afterSpace(src, i) { while (i < src.length && /\s/.test(src[i])) i++; return i; }
function closePair(src, start, left, right) {
  if (src[start] !== left) return -1;
  let depth = 0;
  for (let i = start; i < src.length; i++) {
    if (src[i] === left) depth++;
    else if (src[i] === right && --depth === 0) return i;
  }
  return -1;
}

function splitTopLevel(src, start, end) {
  const result = [];
  let s = start, p = 0, b = 0, a = 0, c = 0;
  for (let i = start; i < end; i++) {
    const x = src[i];
    if (x === '(') p++; else if (x === ')') p--;
    else if (x === '[') b++; else if (x === ']') b--;
    else if (x === '{') c++; else if (x === '}') c--;
    else if (x === '<') a++; else if (x === '>' && a > 0) a--;
    else if (x === ',' && p === 0 && b === 0 && a === 0 && c === 0) {
      result.push({ text: src.slice(s, i), start: s }); s = i+1;
    }
  }
  result.push({text: src.slice(s, end), start: s});
  return result;
}

function lineColumn(src, pos) {
  const before = src.slice(0, pos); const last = before.lastIndexOf('\n');
  return { line: before.split('\n').length, column: pos - last };
}
function createDiagnostic(src, start, path, rule, confidence) {
  return { path, ...lineColumn(src, start), rule, confidence };
}
const IFACE = /\b(IReadOnlyList|IReadOnlyCollection|IEnumerable|ICollection|IList)\s*</g;
const FUNC = /\b([A-Za-z_]\w*)\s*\(/g;
const NAME = /^[A-Za-z_]\w*/;

function interfaceTypeName(src) {
  return /\b(?:IReadOnlyList|IReadOnlyCollection|IEnumerable|ICollection|IList)\s*</.test(src);
}
function detectMethodSignatures(mask) {
  const signatures = new Map();
  for (const match of mask.matchAll(FUNC)) {
    const name = match[1], open = match.index + match[0].lastIndexOf('(');
    const close = closePair(mask, open, '(', ')');
    if (close === -1) continue;
    const tail = afterSpace(mask, close+1);
    if (mask[tail] !== '{' && !mask.startsWith('=>', tail)) continue;
    const lineStart = mask.lastIndexOf('\n', match.index) + 1;
    const prefix = mask.slice(lineStart, match.index).trim();
    if (!/\b(?:void|Task|ValueTask|bool|string|int|long|[A-Z]\w*(?:<[^>]+>)?)\s*$/.test(prefix)) continue;
    const args = splitTopLevel(mask, open+1, close);
    const indices = args.flatMap((a, i) => interfaceTypeName(a.text) ? [i] : []);
    const existing = signatures.get(name);
    if (!existing) signatures.set(name, { indices, ambiguous: false });
    else if (JSON.stringify(existing.indices) !== JSON.stringify(indices)) existing.ambiguous = true;
  }
  return signatures;
}

export function scanCSharp(source, path = 'unknown.cs') {
  const mask = maskCSharpText(source); const found = new Map();
  const record = (pos, rule, confidence='HIGH') => {
    const key = `${pos}|${rule}`;
    if (!found.has(key)) found.set(key, createDiagnostic(source, pos, path, rule, confidence));
  };
  for (const match of mask.matchAll(IFACE)) {
    const open = mask.indexOf('<', match.index); const close = closePair(mask, open, '<', '>');
    if (close === -1) continue;
    let index = afterSpace(mask, close + 1);
    const id = mask.slice(index).match(NAME);
    if (!id) continue;
    index = afterSpace(mask, index + id[0].length);
    if (mask[index] === '=') {
      const next = afterSpace(mask, index + 1);
      if (mask[next] === '[') record(next, 'INTERFACE_INITIALIZER');
      else if (mask[next] === '>' && mask[afterSpace(mask, next+1)] === '[') record(afterSpace(mask,next+1), 'INTERFACE_EXPRESSION_BODY');
    } else if (mask[index] === '{') {
      const end = closePair(mask, index, '{', '}');
      if (end < 0) continue;
      const after = afterSpace(mask, end+1);
      if (mask[after] === '=' && mask[afterSpace(mask, after+1)] === '[') {
        record(afterSpace(mask, after+1), 'INTERFACE_PROPERTY_INITIALIZER');
      }
    } else if (mask[index] === '(') {
      const endParams = closePair(mask, index, '(', ')');
      if (endParams < 0) continue;
      const at = afterSpace(mask, endParams+1);
      if (mask.startsWith('=>', at) && mask[afterSpace(mask, at+2)] === '[') {
        record(afterSpace(mask, at+2), 'INTERFACE_METHOD_EXPRESSION_BODY');
      } else if (mask[at] === '{') {
        const endBody = closePair(mask, at, '{', '}');
        if (endBody < 0) continue;
        const contents = mask.slice(at+1, endBody);
        const ret = /\breturn\s*\[/g;
        for (const m of contents.matchAll(ret)) {
          let depth = 0;
          for (const ch of contents.slice(0,m.index)) { if(ch === '{') depth++; else if(ch === '}') depth--; }
          record(at+1+m.index+m[0].lastIndexOf('['), 'INTERFACE_METHOD_RETURN', depth===0?'HIGH':'REVIEW');
        }
      }
    }
  }
  const signatures = detectMethodSignatures(mask);
  for (const m of mask.matchAll(FUNC)) {
    const entry = signatures.get(m[1]);
    if (!entry || entry.ambiguous) continue;
    const open = m.index+m[0].lastIndexOf('('), close = closePair(mask,open,'(',')');
    if (close < 0) continue;
    const args = splitTopLevel(mask,open+1,close);
    for (const index of entry.indices) {
      if (index >= args.length) continue;
      const arg = args[index];const first = afterSpace(mask, arg.start);
      if (mask[first] === '[') record(first, 'INTERFACE_CALL_ARGUMENT');
    }
  }
  // Named arguments need semantic type resolution. Report them as advisory only.
  const named = /\b[A-Za-z_]\w*\s*:\s*\[/g;
  for (const m of mask.matchAll(named)) {
    const pos = m.index+m[0].lastIndexOf('[');
    if (![...found.values()].some(d=>d.line===lineColumn(source,pos).line && d.column===lineColumn(source,pos).column))
      record(pos,'NAMED_ARGUMENT_REVIEW','REVIEW');
  }
  const findings = [...found.values()].sort((a,b)=>a.line-b.line||a.column-b.column||a.rule.localeCompare(b.rule));
  return { path, status:findings.some(x=>x.confidence==='HIGH')?'REVIEW_REQUIRED':findings.length?'ADVISORY':'CLEAR',
    highConfidence:findings.filter(x=>x.confidence==='HIGH').length, advisory:findings.filter(x=>x.confidence==='REVIEW').length,
    findings };
}

export function scanFiles(files) {
  if (!files || typeof files !== 'object' || Array.isArray(files)) throw new TypeError('Expected {path: source}');
  const results=Object.entries(files).sort((a,b)=>a[0].localeCompare(b[0])).map(([path,source])=>scanCSharp(source,path));
  return { status:!results.length?'INCOMPLETE':results.some(x=>x.highConfidence)?'REVIEW_REQUIRED':results.some(x=>x.advisory)?'ADVISORY':'CLEAR',
    scannedFiles:results.length, highConfidence:results.reduce((a,x)=>a+x.highConfidence,0),
    advisory:results.reduce((a,x)=>a+x.advisory,0), results };
}
