using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class JsonFileSummarizer : IFileSummarizer
    {
        public int Order => 300;

        public bool CanSummarize(FileSummaryContext context)
        {
            return FileTypeInspector.LooksLikeJson(context);
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            string body = JsonSummarizer.Summarize(context.Path, context.Options.Summary.JsonMaxCharacters, cancellationToken);
            return Task.FromResult(new FileSummaryResult("JSON Summary", body));
        }
    }
}

