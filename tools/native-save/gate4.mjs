import { FranchiseFile } from 'madden-franchise';
const open = async p => { const f=new FranchiseFile(p,{autoParse:true,autoUnempty:false});
  await new Promise((r,j)=>{f.on('ready',r);f.on('error',j);}); return f; };

const f = await open(process.argv[2]);
const t = f.getTableByName('Player');
await t.readRecords();

// A real player on the user's team, not an empty slot.
const i = t.records.findIndex(r => !r.isEmpty && r.TeamIndex === 27);
const r = t.records[i];
console.log(`target row ${i}: ${r.FirstName} ${r.LastName}  TeamIndex=${r.TeamIndex} JerseyNum=${r.JerseyNum}`);
const before = r.JerseyNum;
const after = before === 99 ? 98 : 99;
r.JerseyNum = after;
console.log(`JerseyNum ${before} -> ${after}`);
await f.packFile(process.argv[3]);
console.log('written:', process.argv[3]);
