using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class MarkdownFileSummarizer : IFileSummarizer
    {
        private static readonly string[] Extensions = { ".md", ".markdown", ".mdown", ".mkd", ".mkdn" };

        public int Order => 220;

        public bool CanSummarize(FileSummaryContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Extension))
            {
                return false;
            }

            return Extensions.Any(ext => string.Equals(context.Extension, ext, StringComparison.OrdinalIgnoreCase));
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            MarkdownSummaryResult result = MarkdownSummaryService.Summarize(context.Path, context.Options.Summary, cancellationToken);

            string title = $"Markdown Summary ({Path.GetFileName(context.Path)})";

            return Task.FromResult(new FileSummaryResult(
                title,
                result.Summary,
                tables: null,
                result.Preview));
        }
    }
}

