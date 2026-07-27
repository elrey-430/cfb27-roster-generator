/**
 * Writes generated tables back into a dynasty save, producing a NEW save.
 *
 *   node apply.mjs <sourceSave> <destSave> <table.csv> [table.csv ...]
 *
 * Three rules hold this together, and all three exist because the input is
 * somebody's dynasty:
 *
 *   1. **The source is never modified.** The destination is always a new file,
 *      and refusing to write over the source is checked, not assumed.
 *
 *   2. **Only cells that actually differ are written.** Every field is read
 *      back out of the save and compared with the CSV first. This is the same
 *      guarantee the CSV layer has had since Milestone 1 — what comes out
 *      differs only where something was deliberately changed — and it is what
 *      makes the change count in the report a fact rather than a hope.
 *
 *   3. **Empty records are never touched.** A save carries pre-allocated slots
 *      holding no player; the exporter writes blanks for them. Writing those
 *      blanks back would be writing a blank name over a slot the game expects
 *      to find in a particular state.
 *
 * It refuses outright when the save's schema is not the one the CSVs were
 * extracted from. A game patch moves the schema, and a field written at the
 * wrong offset corrupts a dynasty silently.
 */
import fs from 'fs';
import path from 'path';
import { parseRecords } from './csv.mjs';
import { looksLikeSave, openSave, schemaOf, schemaId, tablesNamed } from './save.mjs';

const [, , sourcePath, destPath, ...csvPaths] = process.argv;
if (!sourcePath || !destPath || csvPaths.length === 0) {
  console.error('usage: node apply.mjs <sourceSave> <destSave> <table.csv> [table.csv ...]');
  process.exit(2);
}

if (path.resolve(sourcePath) === path.resolve(destPath)) {
  console.error('Refusing to write over the source save. Give the output a different path.');
  process.exit(4);
}

if (!looksLikeSave(sourcePath)) {
  console.error(`'${sourcePath}' is not a CFB27 dynasty save (no FBCHUNKS header).`);
  process.exit(3);
}

const file = await openSave(sourcePath);
const schema = schemaOf(file);

// The schema guard. The manifest is written beside the CSVs by extract.mjs;
// when it is present its schema must match the save being written.
const manifestPath = path.join(path.dirname(csvPaths[0]), '_native.json');
if (fs.existsSync(manifestPath)) {
  const expected = JSON.parse(fs.readFileSync(manifestPath, 'utf8')).schema;
  if (schemaId(expected) !== schemaId(schema)) {
    console.error(
      `Schema mismatch: these tables came from schema ${schemaId(expected)} but ` +
      `'${path.basename(sourcePath)}' is schema ${schemaId(schema)}. ` +
      'A field written at the wrong offset corrupts a dynasty, so nothing was written.');
    process.exit(5);
  }
}

const report = { source: path.resolve(sourcePath), destination: path.resolve(destPath), schema, tables: [] };

for (const csvPath of csvPaths) {
  const records = parseRecords(fs.readFileSync(csvPath, 'utf8'));
  if (records.length < 2) {
    console.error(`  ${path.basename(csvPath)}: no data rows; skipped.`);
    continue;
  }

  const header = records[0];
  const column = Object.fromEntries(header.map((name, i) => [name, i]));
  for (const required of ['_tableName', '_row']) {
    if (!(required in column)) {
      console.error(`'${csvPath}' has no ${required} column; it is not a table export.`);
      process.exit(6);
    }
  }

  const tableName = records[1][column._tableName];
  const tableIndex = '_tableIndex' in column ? Number(records[1][column._tableIndex]) : null;

  // Several tables share a name, so the index decides when the CSV carries
  // one. Falling back to the name alone would let a 143-team Team table be
  // written over a one-row sentinel that happens to be called Team too.
  const candidates = tablesNamed(file, [tableName]);
  const match = tableIndex !== null
    ? candidates.find(c => c.index === tableIndex)
    : candidates[0];
  if (!match) {
    console.error(`No table '${tableName}'${tableIndex !== null ? ` at index ${tableIndex}` : ''} in this save.`);
    process.exit(7);
  }

  const table = match.table;
  await table.readRecords();

  const fields = header.filter(h => !h.startsWith('_') && table.offsetTable.some(o => o.name === h));
  let changed = 0, skippedEmpty = 0, failed = [];

  for (let r = 1; r < records.length; r++) {
    const row = records[r];
    if (row.length === 1 && row[0] === '') continue;
    const index = Number(row[column._row]);
    const record = table.records[index];
    if (!record) continue;
    if (record.isEmpty) { skippedEmpty++; continue; }

    for (const name of fields) {
      const incoming = row[column[name]] ?? '';
      let current;
      try { current = record[name]; } catch { continue; }
      const currentText = current === undefined || current === null ? '' : String(current);
      if (currentText === incoming) continue;

      try {
        record[name] = typeof current === 'number' ? Number(incoming)
          : typeof current === 'boolean' ? incoming === 'true'
          : incoming;
        changed++;
      } catch (e) {
        failed.push(`row ${index} ${name}: ${e.message}`);
      }
    }
  }

  report.tables.push({
    file: path.basename(csvPath), table: tableName, tableIndex: match.index,
    cellsChanged: changed, emptyRecordsSkipped: skippedEmpty, failures: failed,
  });
  console.error(
    `  ${path.basename(csvPath)} -> ${tableName}[${match.index}]: ` +
    `${changed} cell(s) changed, ${skippedEmpty} empty record(s) left alone` +
    (failed.length ? `, ${failed.length} FAILED` : ''));
  for (const f of failed.slice(0, 10)) console.error(`      ${f}`);
}

await file.packFile(destPath);
report.destinationBytes = fs.statSync(destPath).size;
console.error(`wrote ${destPath} (${report.destinationBytes} bytes)`);
console.log(JSON.stringify(report));
