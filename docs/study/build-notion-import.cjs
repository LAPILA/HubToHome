// Existing study sources -> a native Notion Markdown/CSV import package.
// No game assets, credentials, or Git operations are read or changed.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../..');
const output = path.join(root, 'Library/CodexChecks/StudyNotionImport/package');
const target = 'https://app.notion.com/p/3f6641f6190180bb8567ff9caf83e5a9';
const strict = new TextDecoder('utf-8', {fatal: true});
const read = p => strict.decode(fs.readFileSync(p)).replace(/^\uFEFF/, '');
const layout = JSON.parse(read(path.join(__dirname, 'notion-layout.json')));
const rows = layout.databases.find(x => x.key === 'learning').rows;
const mappings = new Map();
const sources = [];
function add(source, destination) {
  const absolute = path.resolve(root, source);
  mappings.set(absolute, destination);
  sources.push({absolute, destination});
}
const moduleFolder = '02 기능별 학습';
for (const row of rows) {
  const n = row.properties['순서'];
  const title = row.properties['주제'];
  let dest;
  if (n === 1) dest = '01 학습 시작과 전체 구조.md';
  else if (n <= 15) dest = `${moduleFolder}/${String(n-1).padStart(2,'0')} ${title}.md`;
  else if (n === 16) dest = '03 자료구조 알고리즘과 패턴.md';
  else if (n === 17) dest = '04 포트폴리오와 면접 연습.md';
  else if (n === 18) dest = '05 개발 기록과 설계 결정.md';
  else dest = '07 검증 범위와 실습.md';
  add(`docs/study/${row.source}`, dest);
}
add('docs/study/20-code-atlas.md', '06 전체 Scripts 코드 색인.md');
add('docs/study/development-log-index.md', '05 개발 기록과 설계 결정/날짜별 기록 안내.md');
for (const entry of layout.standalonePages.find(x=>x.key==='atlas').children)
  add(`docs/study/${entry.source}`, `06 전체 Scripts 코드 색인/${entry.title}.md`);
for (const name of fs.readdirSync(path.join(root, 'AIAssets')).filter(x=>/^\d{4}-\d{2}-\d{2}-update\.md$/.test(x)))
  add(`AIAssets/${name}`, `05 개발 기록과 설계 결정/${name.slice(0,10)} 개발 기록.md`);
for (const [source,dest] of [['code-inventory.csv','CSharp 전체 파일 목록.csv'],['folder-inventory.csv','Scripts 전체 폴더 목록.csv']])
  mappings.set(path.join(__dirname, source), `06 전체 Scripts 코드 색인/${dest}`);
mappings.set(path.join(__dirname,'README.md'), target);
const write = (dest, text) => {
  const full = path.resolve(output, dest);
  if (!full.startsWith(output + path.sep)) throw new Error('Output escaped package');
  fs.mkdirSync(path.dirname(full), {recursive:true});
  fs.writeFileSync(full, text, {encoding:'utf8'});
};
const relativeLink = (from, to) => /^https?:/.test(to) ? to : encodeURI(path.posix.relative(path.posix.dirname(from),to));
let linkCount = 0;
for (const entry of sources) {
  let body = read(entry.absolute);
  if (/-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|\b(?:ghp_|github_pat_|xoxb-)[A-Za-z0-9_-]{20,}/.test(body))
    throw new Error('Potential credential; inspect source locally before publishing');
  body = body.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (whole,label,raw) => {
    if (/^(?:https?:|mailto:|#)/.test(raw)) return whole;
    const clean = raw.replace(/^<|>$/g,'').split('#')[0];
    const resolved = path.resolve(path.dirname(entry.absolute), decodeURI(clean));
    const mapped = mappings.get(resolved);
    linkCount++;
    if (mapped) return `[${label}](${relativeLink(entry.destination,mapped)})`;
    const relative = path.relative(root, resolved).replaceAll('\\','/');
    return label === path.basename(relative) ? `\`${relative}\`` : `${label} — \`${relative}\``;
  });
  if (entry.absolute.includes(path.sep + 'AIAssets' + path.sep)) {
    body = body.replace(/^# .+\r?\n/, '');
    body = `# ${path.basename(entry.destination,'.md')}\n\n> **당시 작업 기록 보관본** · 현재 동작 검증과 구분해서 읽으세요. 구현 변경·실패·검증의 이력이며 모든 항목을 이번에 재현한 것은 아닙니다.\n\n원본: \`AIAssets/${path.basename(entry.absolute)}\`\n\n${body}`;
  }
  write(entry.destination, body);
}
const moduleRows = rows.filter(x=>x.properties['순서']>=2&&x.properties['순서']<=15);
const hub = '# 기능별 학습 라이브러리\n\n> **기능에서 코드로, 코드에서 설명으로.** 각 문서는 역할 → 데이터 흐름 → 자료구조·패턴 → 실패 경계 → 읽을 파일 → 실습 순서로 구성합니다.\n\n| 기능 | 배우는 개념 |\n| --- | --- |\n' + moduleRows.map(row=>{
  const dest=mappings.get(path.join(__dirname,row.source));
  return `| [${row.properties['주제']}](${relativeLink('02 기능별 학습.md',dest)}) | ${row.properties['키워드'].join(' · ')} |`;
}).join('\n') + '\n\n## 공부 방식\n\n- [ ] 게임에서 해당 현상을 먼저 떠올린다.\n- [ ] 진입 함수와 데이터 소유자를 찾는다.\n- [ ] 정상 경로와 실패 경로를 그린다.\n- [ ] AI 설명을 닫고 본인 말로 설명한다.\n\n**소스 경로**는 저장소 기준입니다. Notion에서 로컬 파일이 열린다고 가정하지 않고 프로젝트에서 경로를 검색하세요.\n';
write('02 기능별 학습.md',hub);
for (const filename of ['code-inventory.csv','folder-inventory.csv']) write(mappings.get(path.join(__dirname,filename)),read(path.join(__dirname,filename)));
const csv = value => '"' + String(value??'').replaceAll('"','""') + '"';
const table = data => data.map(row=>row.map(csv).join(',')).join('\r\n')+'\r\n';
write('08 학습 체크보드.csv',table([['주제','영역','순서','학습 상태','핵심 개념','내 설명','직접 검증'],...rows.map(r=>[r.properties['주제'],r.properties['영역'],r.properties['순서'],'시작 전',r.properties['키워드'].join(' · '),'','아직 수행하지 않음'])]));
write('05 개발 기록과 설계 결정/날짜별 개발 일지.csv',read(path.join(__dirname,'development-log.csv')));
fs.writeFileSync(path.join(root,'Library/CodexChecks/StudyNotionImport/manifest.json'),JSON.stringify({target,createdAt:new Date().toISOString(),sources:sources.map(x=>({source:path.relative(root,x.absolute).replaceAll('\\','/'),file:x.destination})),sourcePages:sources.length,rewrittenLinks:linkCount},null,2));
console.log(JSON.stringify({output,sourcePages:sources.length,generatedHubs:1,csv:4,rewrittenLinks:linkCount}));
