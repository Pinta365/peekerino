using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class ExcelFileSummarizer : IFileSummarizer
    {
        private static readonly string[] SupportedExtensions = { ".xlsx", ".xls", ".xlsm" };

        public int Order => 250;

        public bool CanSummarize(FileSummaryContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Extension))
            {
                return false;
            }

            return SupportedExtensions.Any(ext => string.Equals(context.Extension, ext, StringComparison.OrdinalIgnoreCase));
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            ExcelSummaryResult result = ExcelSummaryService.Summarize(context.Path, context.Options.Summary, cancellationToken);

            return Task.FromResult(new FileSummaryResult(
                "Excel Summary",
                result.Summary,
                result.Tables));
        }
    }
}

