/** Strict XML subset for MSBuild declaration inspection, not evaluation. No DTD or external entities. */
export function inspectXml(source,{allowFragments=false}={}) {
  if(typeof source!=='string'||source.length>8*1024*1024)throw Error('Invalid XML input');
  const nodes=[],stack=[];let roots=0,i=0;
  while(i<source.length){
    if(source.startsWith('<!--',i)){const end=source.indexOf('-->',i+4);if(end<0||source.slice(i+4,end).includes('--'))throw Error('Malformed XML comment');i=end+3;continue;}
    if(source.startsWith('<?xml ',i)&&i===0){const end=source.indexOf('?>',i+6);if(end<0)throw Error('Unclosed XML declaration');i=end+2;continue;}
    if(source[i]!=='<'){const end=source.indexOf('<',i),stop=end<0?source.length:end;const text=source.slice(i,stop);if(stack.length)stack.at(-1).text+=text;else if(text.trim())throw Error('Text outside XML element');i=stop;continue;}
    const token=source.slice(i).match(/^<(\/?)([A-Za-z_][\w:.-]*)(?=\s|\/?>)((?:[^<>"']|"[^"]*"|'[^']*')*)>/);
    if(!token)throw Error('Unsupported or malformed XML tag');
    i+=token[0].length;const [,closing,name,tail]=token;
    if(closing){if(tail.trim()||stack.at(-1)?.name!==name)throw Error('Mismatched XML end tag');stack.pop();continue;}
    const selfClosing=/\/\s*$/.test(tail),body=selfClosing?tail.replace(/\/\s*$/,''):tail;const attrs=Object.create(null);let rest=body;
    while(rest.trim()){const attr=rest.match(/^\s+([A-Za-z_][\w:.-]*)\s*=\s*(["'])(.*?)\2/s);if(!attr||Object.hasOwn(attrs,attr[1]))throw Error('Malformed or duplicate XML attribute');attrs[attr[1]]=attr[3];rest=rest.slice(attr[0].length);}
    if(!stack.length)roots++;
    const parent=stack.at(-1)??null;
    const uncertain=Boolean(parent?.uncertain||Object.hasOwn(attrs,'Condition')||['Choose','When','Otherwise','Target','Import'].includes(name));
    const node={name,attrs,parent:parent?.name??null,uncertain,text:''};nodes.push(node);
    if(!selfClosing)stack.push(node);
  }
  if(stack.length||(!allowFragments&&roots!==1)||!roots)throw Error('Incomplete XML document');
  if(!allowFragments&&nodes[0]?.name!=='Project')throw Error('Expected MSBuild Project root');
  return nodes;
}
export function packageDeclarations(source,{allowFragments=false}={}){
  const nodes=inspectXml(source,{allowFragments});
  return nodes.filter(n=>n.name==='PackageVersion').map(n=>({name:n.attrs.Include??null,version:n.attrs.Version??null,uncertain:n.uncertain||(!allowFragments&&n.parent!=='ItemGroup')||Object.hasOwn(n.attrs,'Update')||Object.hasOwn(n.attrs,'Remove')}));
}
