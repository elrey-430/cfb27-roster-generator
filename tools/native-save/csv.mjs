/**
 * CSV reading and writing that matches RosterGenerator.Core's CsvFormat
 * exactly: comma delimiter, CRLF, no BOM, and a field quoted only when it
 * contains a comma, a quote or a newline.
 *
 * This is deliberately hand-written rather than a dependency. The two sides of
 * this pipeline are a C# writer and a JavaScript reader passing a user's
 * dynasty between them, and a library that "helpfully" normalises quoting or
 * line endings would break the byte-fidelity the whole project rests on.
 */

/** Serialises one record, followed by CRLF. */
export function writeRecord(fields) {
  return fields.map(escapeField).join(',') + '\r\n';
}

function escapeField(value) {
  const s = value === undefined || value === null ? '' : String(value);
  return /[,"\n\r]/.test(s) ? '"' + s.replaceAll('"', '""') + '"' : s;
}

/**
 * Parses CSV text into records of raw field values. Tolerates RFC 4180 quoting
 * and either line ending, because the file may have been through a spreadsheet
 * on its way here.
 */
export function parseRecords(text) {
  if (text.charCodeAt(0) === 0xfeff) text = text.slice(1);

  const records = [];
  let fields = [];
  let field = '';
  let inQuotes = false;
  let i = 0;

  const endField = () => { fields.push(field); field = ''; };
  const endRecord = () => { endField(); records.push(fields); fields = []; };

  while (i < text.length) {
    const c = text[i];
    if (inQuotes) {
      if (c === '"') {
        if (text[i + 1] === '"') { field += '"'; i += 2; continue; }
        inQuotes = false; i++; continue;
      }
      field += c; i++; continue;
    }

    if (c === '"' && field.length === 0) { inQuotes = true; i++; }
    else if (c === ',') { endField(); i++; }
    else if (c === '\r') { endRecord(); i += text[i + 1] === '\n' ? 2 : 1; }
    else if (c === '\n') { endRecord(); i++; }
    else { field += c; i++; }
  }

  if (inQuotes) throw new Error('Unterminated quoted field at end of file.');
  if (field.length > 0 || fields.length > 0) endRecord();
  return records;
}
