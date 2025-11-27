using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using ExcelDataReader;
using Peekerino.Configuration;
using Peekerino.Services.Summaries;

namespace Peekerino.Services
{
    internal static class ExcelSummaryService
    {
        private static bool _encodingRegistered;
        private static readonly object EncodingLock = new();

        internal static ExcelSummaryResult Summarize(
            string path,
            PeekerinoOptions.SummaryOptions options,
            CancellationToken cancellationToken)
        {
            EnsureEncodingRegistered();

            var tables = new List<TableSummary>();
            var overviewBuilder = new StringBuilder();
            var sheetDetailsBuilder = new StringBuilder();

            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                int sheetIndex = 0;
                int previewedSheets = 0;
                bool sheetLimitReached = false;

                int maxSheets = Math.Max(1, options.ExcelMaxSheets);
                int maxPreviewRows = Math.Max(1, options.ExcelMaxRows);
                int maxColumns = Math.Max(1, options.ExcelMaxColumns);
                int maxRowsToScan = Math.Max(maxPreviewRows, options.ExcelMaxRowsToScan);
                int maxCellCharacters = Math.Max(1, options.ExcelMaxCellCharacters);

                do
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    sheetIndex++;
                    string sheetName = string.IsNullOrWhiteSpace(reader.Name)
                        ? $"Sheet {sheetIndex}"
                        : reader.Name;

                    bool includePreview = previewedSheets < maxSheets;
                    if (includePreview)
                    {
                        previewedSheets++;
                    }
                    else
                    {
                        sheetLimitReached = true;
                    }

                    var previewRows = includePreview ? new List<string[]>() : null;
                    bool previewRowsTruncated = false;
                    bool columnLimitHit = false;
                    bool rowScanTruncated = false;
                    int headerColumnCount = 0;
                    long rowsSeen = 0;

                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        rowsSeen++;

                        if (includePreview)
                        {
                            int fieldCount = reader.FieldCount;
                            int columnCount = Math.Min(fieldCount, maxColumns);
                            headerColumnCount = Math.Max(headerColumnCount, columnCount);

                            if (fieldCount > maxColumns)
                            {
                                columnLimitHit = true;
                            }

                            if (previewRows!.Count < maxPreviewRows)
                            {
                                var row = new string[columnCount];
                                for (int i = 0; i < columnCount; i++)
                                {
                                    row[i] = FormatCell(reader.GetValue(i), maxCellCharacters);
                                }

                                previewRows.Add(row);
                            }
                            else
                            {
                                previewRowsTruncated = true;
                            }
                        }

                        if (rowsSeen >= maxRowsToScan)
                        {
                            rowScanTruncated = true;
                            break;
                        }
                    }

                    sheetDetailsBuilder.AppendLine($"{sheetIndex}. {sheetName}");
                    sheetDetailsBuilder.AppendLine($"   Rows scanned: {rowsSeen:N0}{(rowScanTruncated ? $" (limited to {maxRowsToScan:N0})" : string.Empty)}");

                    if (includePreview)
                    {
                        int previewCount = previewRows?.Count ?? 0;
                        sheetDetailsBuilder.AppendLine($"   Preview rows: {previewCount:N0}{(previewRowsTruncated ? $" (limited to {maxPreviewRows:N0})" : string.Empty)}");

                        if (headerColumnCount > 0)
                        {
                            sheetDetailsBuilder.AppendLine($"   Columns shown: {headerColumnCount}{(columnLimitHit ? $" (limited to first {maxColumns})" : string.Empty)}");
                        }

                        if (previewRows is { Count: > 0 })
                        {
                            var headers = CreateHeaders(headerColumnCount);
                            var normalizedRows = NormalizeRows(previewRows, headerColumnCount);
                            tables.Add(new TableSummary(
                                $"{sheetName} (first {Math.Min(previewRows.Count, maxPreviewRows)} rows)",
                                headers,
                                normalizedRows,
                                previewRowsTruncated || rowScanTruncated || columnLimitHit));
                        }
                    }
                    else
                    {
                        sheetDetailsBuilder.AppendLine("   Preview skipped (sheet limit reached).");
                    }

                    sheetDetailsBuilder.AppendLine();
                }
                while (reader.NextResult());

                overviewBuilder.AppendLine($"Sheets detected: {sheetIndex:N0}");
                if (sheetLimitReached)
                {
                    overviewBuilder.AppendLine($"Preview limited to first {maxSheets} sheets.");
                }

                overviewBuilder.AppendLine($"Rows sampled per sheet: up to {maxRowsToScan:N0} (preview shows first {maxPreviewRows:N0}).");
                overviewBuilder.AppendLine($"Columns sampled per sheet: up to {maxColumns}.");
                overviewBuilder.AppendLine();
                overviewBuilder.Append(sheetDetailsBuilder);
            }
            catch (Exception ex)
            {
                overviewBuilder.AppendLine($"Failed to read workbook: {ex.Message}");
            }

            return new ExcelSummaryResult(overviewBuilder.ToString().TrimEnd(), tables);
        }

        private static void EnsureEncodingRegistered()
        {
            if (_encodingRegistered)
            {
                return;
            }

            lock (EncodingLock)
            {
                if (_encodingRegistered)
                {
                    return;
                }

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _encodingRegistered = true;
            }
        }

        private static IReadOnlyList<string> CreateHeaders(int count)
        {
            if (count <= 0)
            {
                return Array.Empty<string>();
            }

            var headers = new string[count];
            for (int i = 0; i < count; i++)
            {
                headers[i] = $"Column {i + 1}";
            }

            return headers;
        }

        private static IReadOnlyList<string[]> NormalizeRows(List<string[]> rows, int columnCount)
        {
            if (columnCount <= 0)
            {
                return rows;
            }

            var normalized = new List<string[]>(rows.Count);

            foreach (var row in rows)
            {
                if (row.Length == columnCount)
                {
                    normalized.Add(row);
                    continue;
                }

                var padded = new string[columnCount];
                Array.Copy(row, padded, Math.Min(row.Length, columnCount));
                normalized.Add(padded);
            }

            return normalized;
        }

        private static string FormatCell(object? value, int maxCharacters)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            string text = value switch
            {
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                double d when double.IsNaN(d) => "NaN",
                double d when double.IsPositiveInfinity(d) => "Infinity",
                double d when double.IsNegativeInfinity(d) => "-Infinity",
                double d => d.ToString("G", CultureInfo.InvariantCulture),
                bool b => b ? "TRUE" : "FALSE",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };

            if (text.Length > maxCharacters)
            {
                return text[..maxCharacters] + "...";
            }

            return text;
        }
    }

    internal sealed class ExcelSummaryResult
    {
        public ExcelSummaryResult(string summary, IReadOnlyList<TableSummary> tables)
        {
            Summary = summary;
            Tables = tables;
        }

        public string Summary { get; }
        public IReadOnlyList<TableSummary> Tables { get; }
    }
}

