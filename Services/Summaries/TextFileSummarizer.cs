using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Peekerino.Services.Summaries
{
    internal sealed class TextFileSummarizer : IFileSummarizer
    {
        public int Order => 900;

        public bool CanSummarize(FileSummaryContext context)
        {
            return FileTypeInspector.IsProbablyTextFile(context);
        }

        public Task<FileSummaryResult> SummarizeAsync(FileSummaryContext context, CancellationToken cancellationToken)
        {
            (string preview, bool truncated) = ReadFileHead(context.Path, context.Options.Summary.TextPreviewBytes);
            var body = $"Preview (first {context.Options.Summary.TextPreviewBytes:N0} bytes):";
            return Task.FromResult(new FileSummaryResult(
                "Text Preview",
                body,
                preview: new TextPreview(Path.GetFileName(context.Path), preview, truncated)));
        }

        private static (string Content, bool Truncated) ReadFileHead(string path, int maxBytes)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: maxBytes, leaveOpen: false);
                var buffer = new char[Math.Min(maxBytes, (int)Math.Min(maxBytes, fs.Length))];
                int read = reader.Read(buffer, 0, buffer.Length);
                string content = new string(buffer, 0, read);
                bool truncated = fs.Length > maxBytes;
                return (content, truncated);
            }
            catch (Exception ex)
            {
                return ($"(Could not read file preview: {ex.Message})", false);
            }
        }
    }
}

