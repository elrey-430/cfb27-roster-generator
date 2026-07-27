/**
 * Shared plumbing for the two sidecars: opening a save, naming its tables the
 * way the export tool names them, and the schema check that decides whether
 * writing is allowed at all.
 */
import { FranchiseFile } from 'madden-franchise';
import fs from 'fs';

/** The bookkeeping columns the export tool puts in front of every table. */
export const BOOKKEEPING = ['_tableIndex', '_tableName', '_row', '_isEmpty'];

/** First bytes of a CFB27 dynasty save. */
const MAGIC = Buffer.from('FBCHUNKS', 'ascii');

/** True when the file starts with the dynasty save magic. */
export function looksLikeSave(path) {
  let fd;
  try {
    fd = fs.openSync(path, 'r');
    const head = Buffer.alloc(MAGIC.length);
    return fs.readSync(fd, head, 0, MAGIC.length, 0) === MAGIC.length && head.equals(MAGIC);
  } catch {
    return false;
  } finally {
    if (fd !== undefined) fs.closeSync(fd);
  }
}

/** Opens a save and waits for it to be parsed. */
export async function openSave(path) {
  const file = new FranchiseFile(path, { autoParse: true, autoUnempty: false });
  await new Promise((resolve, reject) => {
    file.on('ready', resolve);
    file.on('error', reject);
  });
  return file;
}

/** The schema a save was written against, as a plain object. */
export function schemaOf(file) {
  const meta = file.schemaList?.meta ?? {};
  return { major: meta.major, minor: meta.minor, gameYear: meta.gameYear };
}

/** "468.2" — what the schema guard compares. */
export function schemaId(schema) {
  return `${schema.major}.${schema.minor}`;
}

/**
 * The export tool's file name for a table: its position in the table list,
 * zero-padded, then the table's own name. Several tables share a name — there
 * are nine called Team — so the index is what makes the name unique.
 */
export function tableFileName(index, name) {
  return `${String(index).padStart(4, '0')}_${sanitise(name)}.csv`;
}

function sanitise(name) {
  // Table names come out of the save and are not guaranteed to be a legal
  // file name; the export tool's own output shows [] in some of them.
  return (name || 'UnknownTable').replace(/[^A-Za-z0-9_.\-\[\]]/g, '_');
}

/** Every table with one of the given names, with its positional index. */
export function tablesNamed(file, names) {
  const wanted = new Set(names);
  return file.tables
    .map((table, index) => ({ table, index }))
    .filter(entry => wanted.has(entry.table.name));
}

/**
 * Reads a field the way the export tool records it. Empty records hold
 * whatever was last in that memory; the export tool writes blanks for them and
 * this must match, because a blank is the honest answer for a slot that holds
 * no player.
 */
export function readCell(record, column) {
  if (record.isEmpty) return '';
  try {
    const value = record[column];
    return value === undefined || value === null ? '' : String(value);
  } catch {
    return '';
  }
}
