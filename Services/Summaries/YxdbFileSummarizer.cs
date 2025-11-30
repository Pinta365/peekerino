using System;
using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class YxdbFileSummarizer : IFileSummarizer
    {
        public int Order => 260;

        public bool CanSummarize(FileSummaryContext context)
        {
            return !string.IsNullOrWhiteSpace(context.Extension) &&
                   string.Equals(context.Extension, ".yxdb", StringComparison.OrdinalIgnoreCase);
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            YxdbSummaryResult result = YxdbSummaryService.Summarize(context.Path, context.Options.Summary, cancellationToken);

            return Task.FromResult(new FileSummaryResult(
                "YXDB Summary",
                result.Summary,
                result.Tables));
        }
    }
}


