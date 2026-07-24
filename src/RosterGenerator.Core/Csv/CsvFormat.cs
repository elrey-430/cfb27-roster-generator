using System.Text;

namespace RosterGenerator.Core.Csv;

/// <summary>
/// Low-level CSV tokenizer/serializer shared by <see cref="CsvDocument"/>.
/// Reading is RFC 4180-tolerant; writing reproduces the CFB27 export
/// conventions (comma delimiter, CRLF, quote only when required).
/// </summary>
internal static class CsvFormat
{
    /// <summary>Splits CSV text into records of raw field values.</summary>
    internal static List<string[]> ParseRecords(string text)
    {
        // Strip a UTF-8 BOM if one is present so the first header cell
        // compares cleanly. Observed CFB27 exports have no BOM.
        if (text.StartsWith('﻿'))
        {
            text = text[1..];
        }

        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var i = 0;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField();
            records.Add(fields.ToArray());
            fields.Clear();
        }

        while (i < text.Length)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    i++;
                    break;
                case ',':
                    EndField();
                    i++;
                    break;
                case '\r':
                    EndRecord();
                    i += i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                    break;
                case '\n':
                    EndRecord();
                    i++;
                    break;
                default:
                    field.Append(c);
                    i++;
                    break;
            }
        }

        if (inQuotes)
        {
            throw new CsvSchemaException("Unterminated quoted field at end of file.");
        }

        // A final record without a trailing newline still counts.
        if (field.Length > 0 || fields.Count > 0)
        {
            EndRecord();
        }

        return records;
    }

    /// <summary>Appends one record followed by CRLF.</summary>
    internal static void WriteRecord(StringBuilder builder, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendField(builder, fields[i]);
        }

        builder.Append("\r\n");
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        var needsQuoting = value.AsSpan().IndexOfAny(',', '"', '\n') >= 0 || value.Contains('\r');
        if (!needsQuoting)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
    }
}
