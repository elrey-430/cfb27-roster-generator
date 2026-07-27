import { FranchiseFile } from 'madden-franchise';
import fs from 'fs';

const [,, savePath, tableName, outPath] = process.argv;
const file = new FranchiseFile(savePath, { autoParse: true, autoUnempty: false });
await new Promise((res, rej) => { file.on('ready', res); file.on('error', rej); });

const t = file.getTableByName(tableName);
await t.readRecords();

const cols = t.offsetTable.map(o => o.name);
const out = fs.createWriteStream(outPath);
out.write(['_row','_isEmpty', ...cols].join('\t') + '\n');
for (let i = 0; i < t.records.length; i++) {
  const r = t.records[i];
  const cells = cols.map(c => { try { const v = r[c]; return v === undefined || v === null ? '' : String(v); } catch { return '<ERR>'; } });
  out.write([i, r.isEmpty ? 'true' : 'false', ...cells].join('\t') + '\n');
}
await new Promise(res => out.end(res));
console.error(`wrote ${t.records.length} rows x ${cols.length} cols`);
