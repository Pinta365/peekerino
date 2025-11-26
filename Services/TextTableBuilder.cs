using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Peekerino.Services
{
    internal sealed class TextTableBuilder
    {
        private readonly List<string[]> _rows = new();
        private readonly List<int> _widths = new();

        public TextTableBuilder(IEnumerable<string> headers)
        {
            var headerArray = headers.Select(h => h ?? string.Empty).ToArray();
            _rows.Add(headerArray);
            UpdateWidths(headerArray);
        }

        public void AddRow(IEnumerable<string?> cells)
        {
            var row = cells.Select(c => c ?? string.Empty).ToArray();
            _rows.Add(row);
            UpdateWidths(row);
        }

        public void AddSeparator()
        {
            _rows.Add(Array.Empty<string>());
        }

        public string Build()
        {
            var sb = new StringBuilder();
            if (_rows.Count == 0)
            {
                return string.Empty;
            }

            var header = _rows[0];
            sb.AppendLine(FormatRow(header));
            sb.AppendLine(BuildSeparatorLine());

            for (int i = 1; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (row.Length == 0)
                {
                    sb.AppendLine(BuildSeparatorLine());
                }
                else
                {
                    sb.AppendLine(FormatRow(row));
                }
            }

            return sb.ToString().TrimEnd();
        }

        private void UpdateWidths(IReadOnlyList<string> row)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (_widths.Count <= i)
                {
                    _widths.Add(row[i].Length);
                }
                else
                {
                    _widths[i] = Math.Max(_widths[i], row[i].Length);
                }
            }
        }

        private string FormatRow(IReadOnlyList<string> row)
        {
            var parts = new List<string>(row.Count);
            for (int i = 0; i < row.Count; i++)
            {
                string value = row[i];
                int width = i < _widths.Count ? _widths[i] : value.Length;
                parts.Add(value.PadRight(width));
            }

            return "  " + string.Join("  |  ", parts);
        }

        private string BuildSeparatorLine()
        {
            var parts = _widths.Select(width => new string('-', width));
            return "  " + string.Join("--+--", parts);
        }
    }
}

