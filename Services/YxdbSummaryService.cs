using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Peekerino.Configuration;
using Peekerino.Services.Summaries;
using YxdbNet;

namespace Peekerino.Services
{
    internal static class YxdbSummaryService
    {
        internal static YxdbSummaryResult Summarize(
            string path,
            PeekerinoOptions.SummaryOptions options,
            CancellationToken cancellationToken)
        {
            var tables = new List<TableSummary>();
            var builder = new StringBuilder();

            try
            {
                using var reader = YxdbReader.Open(path);
                var header = reader.Header;
                var schema = reader.Schema;

                builder.AppendLine(header.FileDescription ?? "YXDB file");
                builder.AppendLine($"Records declared: {header.RecordCount:N0}");
                if (header.EffectiveRecordCount.HasValue && header.EffectiveRecordCount.Value != header.RecordCount)
                {
                    builder.AppendLine($"Effective records: {header.EffectiveRecordCount.Value:N0}");
                }

                builder.AppendLine($"Created: {header.CreationTime:yyyy-MM-dd HH:mm:ss zzz}");
                builder.AppendLine($"Compressed: {(reader.IsCompressed ? "Yes" : "No")}");
                builder.AppendLine($"Record blocks: {reader.RecordBlockOffsets.Count}");
                builder.AppendLine($"Fields: {schema.Fields.Count:N0}");
                builder.AppendLine();

                AppendSchemaTable(schema.Fields, tables, cancellationToken);
                AppendRowPreview(reader, schema.Fields, options, tables, cancellationToken, header.RecordCount);
            }
            catch (Exception ex)
            {
                builder.AppendLine($"Failed to read YXDB: {ex.Message}");
            }

            return new YxdbSummaryResult(builder.ToString().TrimEnd(), tables);
        }

        private static void AppendSchemaTable(
            IReadOnlyList<YxdbNet.Metadata.YxdbSchemaField> fields,
            List<TableSummary> tables,
            CancellationToken cancellationToken)
        {
            var rows = new List<string[]>(fields.Count);

            foreach (var field in fields)
            {
                cancellationToken.ThrowIfCancellationRequested();

                rows.Add(new[]
                {
                    field.Name,
                    field.Type.ToString(),
                    field.Size?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    field.Scale?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    field.Source ?? string.Empty,
                    field.Description ?? string.Empty
                });
            }

            var headers = new[] { "Name", "Type", "Size", "Scale", "Source", "Description" };
            tables.Add(new TableSummary("Schema", headers, rows, false));
        }

        private static void AppendRowPreview(
            YxdbReader reader,
            IReadOnlyList<YxdbNet.Metadata.YxdbSchemaField> fields,
            PeekerinoOptions.SummaryOptions options,
            List<TableSummary> tables,
            CancellationToken cancellationToken,
            long declaredRecordCount)
        {
            int maxColumns = Math.Max(1, options.YxdbMaxColumns);
            int maxRows = Math.Max(1, options.YxdbMaxRows);
            int maxValueLength = Math.Max(1, options.YxdbMaxValueLength);

            var fieldNames = fields.Select(f => f.Name).Take(maxColumns).ToArray();
            bool columnsTruncated = fields.Count > fieldNames.Length;

            var valueRows = new List<string[]>(maxRows);
            bool rowsTruncated = false;

            foreach (var row in reader.ReadRows())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (valueRows.Count >= maxRows)
                {
                    rowsTruncated = true;
                    break;
                }

                var values = new string[fieldNames.Length];
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    values[i] = FormatValue(row.GetValue(fieldNames[i]), maxValueLength);
                }

                valueRows.Add(values);
            }

            if (valueRows.Count == 0)
            {
                tables.Add(new TableSummary(
                    "Rows preview",
                    fieldNames,
                    Array.Empty<string[]>(),
                    declaredRecordCount > 0));
                return;
            }

            string title = rowsTruncated && declaredRecordCount > 0
                ? $"Rows preview (first {valueRows.Count:N0} of {declaredRecordCount:N0})"
                : $"Rows preview (first {valueRows.Count:N0})";
            tables.Add(new TableSummary(
                title,
                fieldNames,
                valueRows,
                rowsTruncated || columnsTruncated));
        }

        private static string FormatValue(object? value, int maxLength)
        {
            if (value is null)
            {
                return string.Empty;
            }

            string text = value switch
            {
                string s => s,
                bool b => b ? "TRUE" : "FALSE",
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
                byte[] bytes => FormatBinary(bytes),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };

            if (text.Length <= maxLength)
            {
                return text;
            }

            return text[..maxLength] + "...";
        }

        private static string FormatBinary(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "0 bytes";
            }

            int previewLength = Math.Min(8, bytes.Length);
            var prefix = BitConverter.ToString(bytes, 0, previewLength).Replace("-", string.Empty);
            if (bytes.Length <= previewLength)
            {
                return $"0x{prefix} ({bytes.Length} bytes)";
            }

            return $"0x{prefix}... ({bytes.Length} bytes)";
        }
    }

    internal sealed class YxdbSummaryResult
    {
        public YxdbSummaryResult(string summary, IReadOnlyList<TableSummary> tables)
        {
            Summary = summary;
            Tables = tables;
        }

        public string Summary { get; }
        public IReadOnlyList<TableSummary> Tables { get; }
    }
}


