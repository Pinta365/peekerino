using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class XmlFileSummarizer : IFileSummarizer
    {
        public int Order => 100;

        public bool CanSummarize(FileSummaryContext context)
        {
            return FileTypeInspector.LooksLikeXml(context);
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            string body = XmlSummarizer.Summarize(context.Path, cancellationToken);
            return Task.FromResult(new FileSummaryResult("XML Summary", body));
        }
    }
}

