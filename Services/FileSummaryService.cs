using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Peekerino.Configuration;
using Peekerino.Services.Summaries;

namespace Peekerino.Services
{
    public class FileSummaryService
    {
        private readonly IReadOnlyList<IFileSummarizer> _summarizers;
        private readonly PeekerinoOptions _options;

        public FileSummaryService(IEnumerable<IFileSummarizer> summarizers, PeekerinoOptions options)
        {
            _summarizers = summarizers.OrderBy(s => s.Order).ToList();
            _options = options;
        }

        public async Task<FileSummaryResult> BuildSummaryAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new FileSummaryResult("Invalid Path", "No path provided.");
            }

            if (Directory.Exists(path))
            {
                return BuildDirectorySummary(new DirectoryInfo(path));
            }

            if (!File.Exists(path))
            {
                return new FileSummaryResult("Not Found", "Item not found.");
            }

            var fileInfo = new FileInfo(path);
            var context = new FileSummaryContext(path, fileInfo, _options);

            foreach (var summarizer in _summarizers)
            {
                if (!summarizer.CanSummarize(context))
                {
                    continue;
                }

                FileSummaryResult summary = await summarizer.SummarizeAsync(context, cancellationToken);
                return CombineWithMetadata(fileInfo, summary);
            }

            return CombineWithMetadata(fileInfo, new FileSummaryResult("Summary", "No summarizer was able to handle this file."));
        }

        private FileSummaryResult BuildDirectorySummary(DirectoryInfo directory)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Folder: {directory.Name}");
            sb.AppendLine($"Path: {directory.FullName}");
            try
            {
                sb.AppendLine($"Items: {directory.GetFileSystemInfos().Length:N0}");
            }
            catch (UnauthorizedAccessException)
            {
                sb.AppendLine("Items: (access denied)");
            }
            sb.AppendLine($"Last modified: {directory.LastWriteTime}");

            try
            {
                var samples = directory.EnumerateFileSystemInfos().Take(10).ToList();
                if (samples.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Sample contents:");
                    foreach (var item in samples)
                    {
                        sb.AppendLine($"  {(item.Attributes.HasFlag(FileAttributes.Directory) ? "[Dir]" : "[File]")} {item.Name}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                sb.AppendLine();
                sb.AppendLine("Sample contents: (access denied)");
            }

            return new FileSummaryResult("Directory Summary", sb.ToString());
        }

        private static FileSummaryResult CombineWithMetadata(FileInfo info, FileSummaryResult summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"File: {info.Name}");
            sb.AppendLine($"Path: {info.FullName}");
            sb.AppendLine($"Size: {info.Length:N0} bytes");
            sb.AppendLine($"Last modified: {info.LastWriteTime}");

            if (!string.IsNullOrWhiteSpace(summary.Body))
            {
                sb.AppendLine();
                sb.AppendLine(summary.Body);
            }

            return new FileSummaryResult(summary.Title, sb.ToString(), summary.Tables, summary.Preview);
        }
    }
}

