import { FranchiseFile } from 'madden-franchise';
import crypto from 'crypto'; import fs from 'fs';
const open = async p => { const f=new FranchiseFile(p,{autoParse:true,autoUnempty:false});
  await new Promise((r,j)=>{f.on('ready',r);f.on('error',j);}); return f; };
const h = x => crypto.createHash('sha256').update(x).digest('hex');

for (const name of ['DYNASTY-BASE1','DYNASTY-BASE2','DYNASTY-BASE3','DYNASTY-BASE4','DYNASTY-BASE5']) {
  const src = `saves/${name}`, tmp = `/tmp/rt_${name}`;
  try {
    const a = await open(src);
    const meta = a.schemaList?.meta;
    await a.packFile(tmp);
    const b = await open(tmp);
    const same = a.unpackedFileContents.equals(b.unpackedFileContents);
    const p = fs.statSync(src).size, q = fs.statSync(tmp).size;
    console.log(`${name}  year=${a.gameYear} schema=${meta.major}.${meta.minor} tables=${a.tables.length} ` +
                `packed ${p}->${q} unpacked=${a.unpackedFileContents.length} ROUNDTRIP=${same ? 'IDENTICAL' : 'DIFFERS'}`);
    fs.unlinkSync(tmp);
  } catch (e) { console.log(`${name}  FAILED: ${e.message}`); }
}
