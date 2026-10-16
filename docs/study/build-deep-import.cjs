// Mechanical expansion of reviewed documentation templates; does not modify game files.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '../..');
const deep = path.join(__dirname, 'deep');
const src = path.join(deep, 'source');
const packageRoot = path.join(root, 'Library/CodexChecks/StudyDeepImport/package');
const evidencePath = path.join(deep, 'source-evidence.json');
const check = process.argv.includes('--check');
const hash = b => crypto.createHash('sha256').update(b).digest('hex');
const read = p => new TextDecoder('utf-8', {fatal:true}).decode(fs.readFileSync(p)).replace(/^\uFEFF/, '');
const templates = fs.readdirSync(src).filter(x=>x.endsWith('.md.in')).sort();
const pages = [];
const evidence = [];
for (const template of templates) {
  const name = template.slice(0,-3);
  let count=0;
  let body=read(path.join(src,template)).replace(/^@@source (Assets\/_Game\/Scripts\/[^\n#]+)#L(\d+)-L(\d+)\s*$/gm, (_,source,start,end)=>{
    const file=path.resolve(root,source);
    if (!file.startsWith(path.join(root,'Assets/_Game/Scripts')+path.sep)) throw new Error('Unsafe source '+source);
    const bytes=fs.readFileSync(file), lines=read(file).split(/\r?\n/);
    start=Number(start);end=Number(end);
    if(start<1||end<start||end>lines.length) throw new Error('Invalid excerpt range '+source);
    const excerpt=lines.slice(start-1,end).join('\n');
    evidence.push({document:name,id:`${name}:${++count}`,source,start,end,fileSha256:hash(bytes),excerptSha256:hash(excerpt)});
    return `근거 · \`${source}\` · ${start}–${end}행\n\n\`\`\`csharp\n${excerpt}\n\`\`\``;
  });
  if(body.includes('@@source')) throw new Error('Unresolved excerpt '+name);
  if((body.match(/^```/gm)||[]).length%2) throw new Error('Unclosed fence '+name);
  if(!body.startsWith('# ')||body.length<1000) throw new Error('Incomplete article '+name);
  if(/-----BEGIN .*PRIVATE KEY-----|\b(?:ghp_|github_pat_|xoxb-)[A-Za-z0-9_-]{20,}/.test(body)) throw new Error('Potential secret');
  for(const m of body.matchAll(/\[[^\]]+\]\(([^)]+)\)/g)) {
    if(/^https?:|^#/.test(m[1])) continue;
    const target=decodeURI(m[1].split('#')[0]);
    if(!templates.includes(target+'.in')) throw new Error(`Broken link ${name}: ${target}`);
  }
  pages.push({name,body,excerpts:count});
}
const record={date:'2026-10-12',basis:'working tree including uncommitted changes',pages:pages.map(p=>({file:p.name,characters:p.body.length,excerpts:p.excerpts})),excerpts:evidence};
const destinations=new Map(pages.map(p=>[p.name,p.name==='README.md'?'코드 따라 읽기.md':`코드 따라 읽기/${p.body.split('\n')[0].slice(2)}.md`]));
const nativeBody=p=>p.body.replace(/\[([^\]]+)\]\(([^)]+)\)/g,(whole,label,target)=>{
  if(!destinations.has(target)) return whole;
  return `[${label}](${encodeURI(path.posix.relative(path.posix.dirname(destinations.get(p.name)),destinations.get(target)))})`;
});
if(check){
  const previous=JSON.parse(read(evidencePath));
  if(JSON.stringify(previous)!==JSON.stringify(record)) throw new Error('Sources/templates changed; regenerate before publication');
  for(const p of pages) {
    if(read(path.join(deep,p.name))!==p.body) throw new Error('Generated document differs: '+p.name);
    if(read(path.join(packageRoot,destinations.get(p.name)))!==nativeBody(p)) throw new Error('Package differs: '+p.name);
  }
} else {
  fs.mkdirSync(packageRoot,{recursive:true});
  for(const p of pages){
    fs.writeFileSync(path.join(deep,p.name),p.body,'utf8');
    const dest=path.join(packageRoot,destinations.get(p.name));
    fs.mkdirSync(path.dirname(dest),{recursive:true});
    fs.writeFileSync(dest,nativeBody(p),'utf8');
  }
  fs.writeFileSync(evidencePath,JSON.stringify(record,null,2)+'\n','utf8');
}
console.log(JSON.stringify({mode:check?'validated':'generated',pages:pages.length,excerpts:evidence.length,sourceFiles:new Set(evidence.map(x=>x.source)).size,characters:pages.reduce((n,p)=>n+p.body.length,0),packageRoot}));
