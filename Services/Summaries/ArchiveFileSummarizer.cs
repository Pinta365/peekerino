using System.Threading;
using System.Threading.Tasks;
using Peekerino.Configuration;

namespace Peekerino.Services.Summaries
{
    internal sealed class ArchiveFileSummarizer : IFileSummarizer
    {
        public int Order => 400;

        public bool CanSummarize(FileSummaryContext context)
        {
            return FileTypeInspector.DetectArchiveFormat(context) != ArchiveFormat.None;
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            var format = FileTypeInspector.DetectArchiveFormat(context);
            if (format == ArchiveFormat.None)
            {
                return Task.FromResult(new FileSummaryResult("Archive", "Unsupported archive format."));
            }

            ArchiveSummaryResult result = ArchiveSummaryService.Summarize(context.Path, format, context.Options.Summary);

            var tables = result.Table != null
                ? new[] { result.Table }
                : null;

            return Task.FromResult(new FileSummaryResult(
                $"{format} Summary",
                result.Summary,
                tables,
                result.Preview));
        }
    }
}

