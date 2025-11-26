using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class CsvFileSummarizer : IFileSummarizer
    {
        public int Order => 200;

        public bool CanSummarize(FileSummaryContext context)
        {
            return string.Equals(context.Extension, ".csv", System.StringComparison.OrdinalIgnoreCase);
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            CsvSummaryResult summary = CsvSummarizer.Summarize(context.Path, cancellationToken);

            var table = new TableSummary(
                "CSV Preview",
                summary.Headers,
                summary.Rows,
                summary.PreviewTruncated);

            return Task.FromResult(new FileSummaryResult(
                "CSV Summary",
                summary.SummaryText,
                new[] { table }));
        }
    }
}

