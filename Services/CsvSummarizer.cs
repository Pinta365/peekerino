using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Peekerino.Services
{
    internal static class CsvSummarizer
    {
        internal static CsvSummaryResult Summarize(string path, CancellationToken cancellationToken = default)
        {
            var summary = new StringBuilder();
            var headers = new List<string>();
            var previewRows = new List<string[]>();
            long rowCount = 0;
            bool truncated = false;

            try
            {
                using var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                string? headerLine = reader.ReadLine();
                if (headerLine == null)
                {
                    summary.AppendLine("CSV appears empty.");
                    return new CsvSummaryResult(headers, previewRows, summary.ToString(), rowCount, truncated);
                }

                char delimiter = DetectDelimiter(headerLine);
                headers.AddRange(SplitCsvLine(headerLine, delimiter));
                summary.AppendLine($"Detected delimiter: {DescribeDelimiter(delimiter)}");
                summary.AppendLine("CSV Columns:");
                for (int i = 0; i < headers.Count; i++)
                {
                    var name = string.IsNullOrWhiteSpace(headers[i]) ? $"Column {i + 1}" : headers[i];
                    summary.AppendLine($"  {i + 1}. {name}");
                    headers[i] = name;
                }

                const long maxRowsToInspect = 5000;
                const int maxPreviewRows = 100;
                var numericColumns = new Dictionary<int, NumericStats>();

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rowCount++;

                    var cells = SplitCsvLine(line, delimiter);

                    if (previewRows.Count < maxPreviewRows)
                    {
                        previewRows.Add(ToRowArray(headers.Count, cells));
                    }

                    for (int i = 0; i < headers.Count && i < cells.Count; i++)
                    {
                        if (decimal.TryParse(cells[i], NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                        {
                            if (!numericColumns.TryGetValue(i, out var stats))
                            {
                                stats = new NumericStats();
                                numericColumns[i] = stats;
                            }
                            stats.Push(value);
                        }
                    }

                    if (rowCount >= maxRowsToInspect)
                    {
                        truncated = true;
                        break;
                    }
                }

                summary.AppendLine();
                summary.AppendLine($"Data rows scanned: {rowCount:N0}");
                if (previewRows.Count >= maxPreviewRows || truncated)
                {
                    summary.AppendLine($"Preview rows shown: {previewRows.Count:N0} (limited to first {maxPreviewRows:N0})");
                }
                else
                {
                    summary.AppendLine($"Preview rows shown: {previewRows.Count:N0}");
                }

                if (numericColumns.Count > 0)
                {
                    summary.AppendLine();
                    summary.AppendLine("Numeric column stats (sample):");
                    foreach (var kv in numericColumns)
                    {
                        string columnName = kv.Key < headers.Count ? headers[kv.Key] : $"Column {kv.Key + 1}";
                        summary.AppendLine($"  {columnName}: min {kv.Value.Min}, max {kv.Value.Max}, avg {kv.Value.Average:N2}");
                    }
                }

                if (truncated)
                {
                    summary.AppendLine();
                    summary.AppendLine($"Processing stopped early after {rowCount:N0} rows for performance.");
                }
            }
            catch (Exception ex)
            {
                summary.AppendLine($"Error summarizing CSV: {ex.Message}");
            }

            return new CsvSummaryResult(headers, previewRows, summary.ToString(), rowCount, truncated);
        }

        private static List<string> SplitCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result;
        }

        private static char DetectDelimiter(string line)
        {
            var candidates = new[] { ',', ';', '\t', '|', ':' };
            char bestDelimiter = ',';
            int bestCount = -1;

            foreach (var candidate in candidates)
            {
                int count = CountOccurrences(line, candidate);
                if (count > bestCount)
                {
                    bestCount = count;
                    bestDelimiter = candidate;
                }
            }

            return bestCount > 0 ? bestDelimiter : ',';
        }

        private static int CountOccurrences(string line, char delimiter)
        {
            bool inQuotes = false;
            int count = 0;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    count++;
                }
            }

            return count;
        }

        private static string DescribeDelimiter(char delimiter) =>
            delimiter switch
            {
                '\t' => "Tab (\\t)",
                ';' => "Semicolon (;)",
                '|' => "Pipe (|)",
                ':' => "Colon (:)",
                ',' => "Comma (,)",
                _ => delimiter.ToString()
            };

        private static string[] ToRowArray(int columnCount, IList<string> cells)
        {
            var row = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                row[i] = i < cells.Count ? cells[i] : string.Empty;
            }
            return row;
        }

        private sealed class NumericStats
        {
            public decimal Min { get; private set; } = decimal.MaxValue;
            public decimal Max { get; private set; } = decimal.MinValue;
            public decimal Sum { get; private set; }
            public long Count { get; private set; }

            public void Push(decimal value)
            {
                if (value < Min)
                {
                    Min = value;
                }

                if (value > Max)
                {
                    Max = value;
                }

                Sum += value;
                Count++;
            }

            public decimal Average => Count == 0 ? 0 : Sum / Count;
        }
    }
}

