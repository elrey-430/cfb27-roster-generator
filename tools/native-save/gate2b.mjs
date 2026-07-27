import { FranchiseFile } from 'madden-franchise';
import crypto from 'crypto';
const open = async p => { const f=new FranchiseFile(p,{autoParse:true,autoUnempty:false});
  await new Promise((r,j)=>{f.on('ready',r);f.on('error',j);}); return f; };
const h = x => crypto.createHash('sha256').update(x).digest('hex');

const a = await open(process.argv[2]), b = await open(process.argv[3]);
const ua = a.unpackedFileContents, ub = b.unpackedFileContents;
console.log(`original  unpacked: ${ua.length} bytes  sha=${h(ua).slice(0,32)}`);
console.log(`repacked  unpacked: ${ub.length} bytes  sha=${h(ub).slice(0,32)}`);
console.log('UNPACKED DB BYTE-IDENTICAL:', ua.equals(ub));
if (!ua.equals(ub)) {
  let first=-1,n=0; const m=Math.min(ua.length,ub.length);
  for(let i=0;i<m;i++) if(ua[i]!==ub[i]){ if(first<0)first=i; n++; }
  console.log(`  first diff at ${first}, ${n} bytes differ (${(100*n/m).toFixed(4)}%)`);
}
console.log('tables:', a.tables.length, 'vs', b.tables.length);
console.log('schema:', JSON.stringify(a.schemaList?.meta), 'vs', JSON.stringify(b.schemaList?.meta));
