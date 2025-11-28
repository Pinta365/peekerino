using System;
using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class AlteryxWorkflowSummarizer : IFileSummarizer
    {
        private static readonly string[] SupportedExtensions = { ".yxmd", ".yxmc", ".yxwz" };

        public int Order => 90;

        public bool CanSummarize(FileSummaryContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Extension))
            {
                return false;
            }

            foreach (var ext in SupportedExtensions)
            {
                if (string.Equals(context.Extension, ext, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            AlteryxWorkflowSummary summary = AlteryxWorkflowSummaryService.Summarize(context.Path);

            var result = new FileSummaryResult(
                "Alteryx Workflow Summary",
                summary.Summary,
                summary.Tables);

            return Task.FromResult(result);
        }
    }
}

