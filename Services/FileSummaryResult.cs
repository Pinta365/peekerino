using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Peekerino.Services
{
    public sealed class FileSummaryResult
    {
        public FileSummaryResult(
            string title,
            string body,
            IEnumerable<TableSummary>? tables = null,
            TextPreview? preview = null)
        {
            Title = title;
            Body = body;
            Tables = new ReadOnlyCollection<TableSummary>((tables ?? Enumerable.Empty<TableSummary>()).ToList());
            Preview = preview;
        }

        public string Title { get; }
        public string Body { get; }
        public IReadOnlyList<TableSummary> Tables { get; }
        public TextPreview? Preview { get; }

        public string SummaryText
        {
            get
            {
                if (Preview == null || string.IsNullOrWhiteSpace(Preview.Content))
                {
                    return Body;
                }

                return $"{Body}{System.Environment.NewLine}{System.Environment.NewLine}Preview: {Preview.Title}{System.Environment.NewLine}{Preview.Content}{(Preview.IsTruncated ? System.Environment.NewLine + "... (truncated preview)" : string.Empty)}";
            }
        }
    }

    public sealed class TableSummary
    {
        public TableSummary(string title, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, bool isTruncated)
        {
            Title = title;
            Headers = headers;
            Rows = rows;
            IsTruncated = isTruncated;
        }

        public string Title { get; }
        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<string[]> Rows { get; }
        public bool IsTruncated { get; }
    }

    public sealed class TextPreview
    {
        public TextPreview(string title, string content, bool isTruncated)
        {
            Title = title;
            Content = content;
            IsTruncated = isTruncated;
        }

        public string Title { get; }
        public string Content { get; }
        public bool IsTruncated { get; }
    }

    internal sealed class CsvSummaryResult
    {
        public CsvSummaryResult(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows, string summaryText, long rowsScanned, bool previewTruncated)
        {
            Headers = headers;
            Rows = rows;
            SummaryText = summaryText;
            RowsScanned = rowsScanned;
            PreviewTruncated = previewTruncated;
        }

        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<string[]> Rows { get; }
        public string SummaryText { get; }
        public long RowsScanned { get; }
        public bool PreviewTruncated { get; }
    }
}
