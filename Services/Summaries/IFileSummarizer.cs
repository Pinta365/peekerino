using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    public interface IFileSummarizer
    {
        int Order { get; }

        bool CanSummarize(FileSummaryContext context);

        Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken);
    }
}

