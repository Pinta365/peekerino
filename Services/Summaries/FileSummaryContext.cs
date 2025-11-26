using System.IO;
using Peekerino.Configuration;

namespace Peekerino.Services.Summaries
{
    public sealed class FileSummaryContext
    {
        public FileSummaryContext(string path, FileInfo fileInfo, PeekerinoOptions options)
        {
            Path = path;
            FileInfo = fileInfo;
            Options = options;
        }

        public string Path { get; }
        public FileInfo FileInfo { get; }
        public string Extension => FileInfo.Extension;
        public PeekerinoOptions Options { get; }
    }
}

