/**
 * Reads a native CFB27 dynasty save and writes the tables the generator needs
 * as CSVs, laid out and named the way the community export tool lays them out.
 *
 * The point is that nothing downstream has to know a save was involved. What
 * comes out of here is what the rest of the pipeline has read since Milestone
 * 3, so the save becomes a third kind of input rather than a second kind of
 * program.
 *
 * Only the tables asked for are written. A full export is 2,272 tables and
 * ~27 MB of CSV; the generator reads three of them.
 *
 *   node extract.mjs <save> <outDir> [Player,Team,CharacterVisuals]
 */
import fs from 'fs';
import path from 'path';
import { writeRecord } from './csv.mjs';
import { BOOKKEEPING, looksLikeSave, openSave, schemaOf, tableFileName, tablesNamed, readCell }
  from './save.mjs';

const [, , savePath, outDir, tableList] = process.argv;
if (!savePath || !outDir) {
  console.error('usage: node extract.mjs <save> <outDir> [Table,Table,...]');
  process.exit(2);
}

// Every table the generator discovers by name. All nine tables called Team are
// written, not just the big one: picking the right one is the reader's job and
// it already does it, so this must not quietly make that decision here.
const names = (tableList || 'Player,Team,CharacterVisuals').split(',').map(s => s.trim()).filter(Boolean);

// Told apart from the header check below: a save that is not there and a file
// that is not a save are different problems, and "no FBCHUNKS header" is a
// baffling thing to be told about a file nobody pointed at. Relative paths
// resolve against this process's working directory, not the caller's.
if (!fs.existsSync(savePath)) {
  console.error(`There is no dynasty save at '${path.resolve(savePath)}'.`);
  process.exit(3);
}

if (!looksLikeSave(savePath)) {
  console.error(`'${savePath}' is not a CFB27 dynasty save (no FBCHUNKS header).`);
  process.exit(3);
}

const file = await openSave(savePath);
const schema = schemaOf(file);
fs.mkdirSync(outDir, { recursive: true });

const written = [];
for (const { table, index } of tablesNamed(file, names)) {
  await table.readRecords();
  const columns = table.offsetTable.map(o => o.name);
  const fileName = tableFileName(index, table.name);

  const out = fs.createWriteStream(path.join(outDir, fileName));
  out.write(writeRecord([...BOOKKEEPING, ...columns]));
  for (let row = 0; row < table.records.length; row++) {
    const record = table.records[row];
    out.write(writeRecord([
      index, table.name, row, record.isEmpty ? 'true' : 'false',
      ...columns.map(c => readCell(record, c)),
    ]));
  }
  await new Promise(resolve => out.end(resolve));

  written.push({
    tableIndex: index, name: table.name, fileName,
    recordCount: table.records.length,
    emptyRecordCount: table.records.filter(r => r.isEmpty).length,
    fieldCount: columns.length,
  });
}

// Written beside the CSVs so the apply step can refuse a save that is not the
// one these tables came from, and refuse a game patch that moved the schema.
const manifest = {
  producer: 'cfb27-roster-generator native-save extract',
  source: path.resolve(savePath),
  sourceBytes: fs.statSync(savePath).size,
  extractedAt: new Date().toISOString(),
  gameYear: file.gameYear,
  schema,
  tables: written,
};
fs.writeFileSync(path.join(outDir, '_native.json'), JSON.stringify(manifest, null, 2));

console.error(
  `extracted ${written.length} table(s) from ${path.basename(savePath)} ` +
  `(schema ${schema.major}.${schema.minor}, gameYear ${schema.gameYear})`);
for (const t of written) {
  console.error(`  ${t.fileName}  ${t.recordCount} records x ${t.fieldCount} fields`);
}
console.log(JSON.stringify(manifest));
