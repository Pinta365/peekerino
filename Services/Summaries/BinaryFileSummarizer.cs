using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class BinaryFileSummarizer : IFileSummarizer
    {
        public int Order => 1000;

        public bool CanSummarize(FileSummaryContext context) => true;

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            string body = BinarySummaryService.Summarize(context.Path, context.Options.Summary);
            return Task.FromResult(new FileSummaryResult("Binary Summary", body));
        }
    }
}

