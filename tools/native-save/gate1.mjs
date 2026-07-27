import { FranchiseFile } from 'madden-franchise';

const path = process.argv[2];
const file = new FranchiseFile(path, { schemaOverride: false, autoParse: true, autoUnempty: false });

await new Promise((res, rej) => { file.on('ready', res); file.on('error', rej); });

console.log('gameYear      :', file.gameYear);
console.log('gameType      :', file.openedFranchiseFile?.gameType ?? file.gameType);
console.log('schema        :', file.schemaList?.meta ?? file.schema?.meta);
console.log('packed size   :', file.packedFileContents?.length);
console.log('unpacked size :', file.unpackedFileContents?.length);
console.log('tables        :', file.tables?.length);
