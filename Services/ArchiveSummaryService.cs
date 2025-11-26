using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Formats.Tar;
using System.Linq;
using System.Text;

namespace Peekerino.Services
{
    internal static class ArchiveSummaryService
    {
        public static ArchiveSummaryResult Summarize(string path, ArchiveFormat format, Configuration.PeekerinoOptions.SummaryOptions options)
        {
            return format switch
            {
                ArchiveFormat.Zip => SummarizeZip(path, options),
                ArchiveFormat.Tar => SummarizeTar(path, TarCompression.None, options),
                ArchiveFormat.TarGz => SummarizeTar(path, TarCompression.Gzip, options),
                ArchiveFormat.GZipSingle => SummarizeGZip(path, options),
                _ => new ArchiveSummaryResult($"Unsupported archive format for {Path.GetFileName(path)}.")
            };
        }

        private static ArchiveSummaryResult SummarizeZip(string path, Configuration.PeekerinoOptions.SummaryOptions options)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

                if (archive.Entries.Count == 0)
                {
                    return new ArchiveSummaryResult("Archive is empty.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Entries: {archive.Entries.Count:N0}");
                sb.AppendLine($"Total uncompressed size: {archive.Entries.Sum(e => e.Length):N0} bytes");
                sb.AppendLine($"Total compressed size: {archive.Entries.Sum(e => e.CompressedLength):N0} bytes");
                sb.AppendLine();

                var tableRows = new List<string[]>();
                var table = new TextTableBuilder(new[] { "Name", "Size", "Compressed", "Ratio", "Modified" });
                foreach (var entry in archive.Entries.Take(options.ArchiveMaxEntries))
                {
                    var row = new[]
                    {
                        entry.FullName,
                        entry.Length.ToString("N0"),
                        entry.CompressedLength.ToString("N0"),
                        entry.Length == 0 ? "-" : $"{(1.0 - (double)entry.CompressedLength / entry.Length):P0}",
                        entry.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
                    };
                    table.AddRow(row);
                    tableRows.Add(row);
                }
                sb.AppendLine(table.Build());

                bool truncated = archive.Entries.Count > options.ArchiveMaxEntries;
                if (truncated)
                {
                    sb.AppendLine();
                    sb.AppendLine($"... {archive.Entries.Count - options.ArchiveMaxEntries:N0} more entries");
                }

                var textEntry = archive.Entries.FirstOrDefault(e => IsPreviewableTextEntry(e.FullName) && e.Length > 0);
                TextPreview? preview = null;
                if (textEntry != null)
                {
                    using var entryStream = textEntry.Open();
                    var previewInfo = ReadTextPreview(entryStream, options.ArchivePreviewBytes);
                    preview = new TextPreview(textEntry.FullName, previewInfo.Content, previewInfo.Truncated);

                    sb.AppendLine();
                    sb.AppendLine($"Preview entry: {textEntry.FullName}{(previewInfo.Truncated ? " (truncated)" : string.Empty)}");
                }

                var tableSummary = new TableSummary(
                    "Archive Entries",
                    new[] { "Name", "Size", "Compressed", "Ratio", "Modified" },
                    tableRows,
                    truncated);

                return new ArchiveSummaryResult(sb.ToString().TrimEnd(), tableSummary, preview);
            }
            catch (Exception ex)
            {
                return new ArchiveSummaryResult($"Archive summary failed: {ex.Message}");
            }
        }

        private static ArchiveSummaryResult SummarizeTar(string path, TarCompression compression, Configuration.PeekerinoOptions.SummaryOptions options)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Stream tarStream = compression switch
                {
                    TarCompression.Gzip => new GZipStream(fs, CompressionMode.Decompress, leaveOpen: false),
                    _ => fs
                };

                using var reader = new TarReader(tarStream, leaveOpen: false);
                var entries = new List<TarEntryInfo>();
                string? previewName = null;
                string? previewText = null;
                bool previewTruncated = false;
                long totalSize = 0;
                int count = 0;

                TarEntry? entry;
                while ((entry = reader.GetNextEntry()) != null)
                {
                    count++;
                    totalSize += entry.Length;

                    if (entries.Count < options.ArchiveMaxEntries)
                    {
                        entries.Add(new TarEntryInfo(
                            entry.Name,
                            entry.Length,
                            entry.EntryType.ToString(),
                            entry.ModificationTime));
                    }

                    if (previewText == null &&
                        entry.EntryType == TarEntryType.RegularFile &&
                        entry.Length > 0 &&
                        IsPreviewableTextEntry(entry.Name) &&
                        entry.DataStream != null)
                    {
                        previewName = entry.Name;
                        var previewInfo = ReadTextPreview(entry.DataStream, options.ArchivePreviewBytes);
                        previewText = previewInfo.Content;
                        previewTruncated = previewInfo.Truncated;
                    }
                    else if (entry.DataStream != null)
                    {
                        DrainStream(entry.DataStream);
                    }
                }

                if (count == 0)
                {
                    return new ArchiveSummaryResult("Archive is empty.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Entries: {count:N0}");
                sb.AppendLine($"Total size: {totalSize:N0} bytes");
                sb.AppendLine();

                var table = new TextTableBuilder(new[] { "Name", "Size", "Type", "Modified" });
                var rows = new List<string[]>();
                foreach (var info in entries)
                {
                    table.AddRow(new[]
                    {
                        info.Name,
                        info.Size.ToString("N0"),
                        info.Type,
                        info.Modified.ToString("yyyy-MM-dd HH:mm")
                    });
                    rows.Add(new[]
                    {
                        info.Name,
                        info.Size.ToString("N0"),
                        info.Type,
                        info.Modified.ToString("yyyy-MM-dd HH:mm")
                    });
                }
                sb.AppendLine(table.Build());

                bool truncated = count > options.ArchiveMaxEntries;
                if (truncated)
                {
                    sb.AppendLine();
                    sb.AppendLine($"... {count - options.ArchiveMaxEntries:N0} more entries");
                }

                TextPreview? preview = null;
                if (previewText != null)
                {
                    preview = new TextPreview(previewName ?? "Preview", previewText, previewTruncated);

                    sb.AppendLine();
                    sb.AppendLine($"Preview entry: {previewName}{(previewTruncated ? " (truncated)" : string.Empty)}");
                }

                var tableSummary = new TableSummary(
                    "Archive Entries",
                    new[] { "Name", "Size", "Type", "Modified" },
                    rows,
                    truncated);

                return new ArchiveSummaryResult(sb.ToString().TrimEnd(), tableSummary, preview);
            }
            catch (Exception ex)
            {
                return new ArchiveSummaryResult($"Archive summary failed: {ex.Message}");
            }
        }

        private static ArchiveSummaryResult SummarizeGZip(string path, Configuration.PeekerinoOptions.SummaryOptions options)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                long compressedSize = fs.Length;
                long uncompressedSize = TryReadGZipUncompressedSize(fs);
                fs.Seek(0, SeekOrigin.Begin);

                using var gzip = new GZipStream(fs, CompressionMode.Decompress, leaveOpen: false);
                var previewInfo = ReadTextPreview(gzip, options.ArchivePreviewBytes);

                var sb = new StringBuilder();
                sb.AppendLine("Single compressed stream (.gz)");
                sb.AppendLine($"Compressed size: {compressedSize:N0} bytes");
                if (uncompressedSize >= 0)
                {
                    sb.AppendLine($"Reported uncompressed size: {uncompressedSize:N0} bytes");
                }
                sb.AppendLine();
                sb.AppendLine($"Preview available ({(previewInfo.Truncated ? "truncated" : "full")})");

                return new ArchiveSummaryResult(
                    sb.ToString().TrimEnd(),
                    preview: new TextPreview(Path.GetFileName(path), previewInfo.Content, previewInfo.Truncated));
            }
            catch (Exception ex)
            {
                return new ArchiveSummaryResult($"GZip summary failed: {ex.Message}");
            }
        }

        private static bool IsPreviewableTextEntry(string name)
        {
            string extension = Path.GetExtension(name).ToLowerInvariant();
            return extension is ".txt" or ".json" or ".csv" or ".md" or ".xml" or ".yml" or ".yaml" or ".ini" or ".log";
        }

        private static (string Content, bool Truncated) ReadTextPreview(Stream stream, int maxBytes)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                string text = DecodeText(buffer, read);
                DrainStream(stream);

                if (string.IsNullOrEmpty(text))
                {
                    return ("(entry appears binary or empty)", false);
                }

                bool truncated = read == maxBytes;
                return (text, truncated);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static string DecodeText(byte[] buffer, int count)
        {
            if (count <= 0)
            {
                return string.Empty;
            }

            if (count >= 2)
            {
                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                {
                    return Encoding.Unicode.GetString(buffer, 0, count);
                }
                if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(buffer, 0, count);
                }
            }
            if (count >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(buffer, 0, count);
            }

            return Encoding.UTF8.GetString(buffer, 0, count);
        }

        private static void DrainStream(Stream? stream)
        {
            if (stream == null)
            {
                return;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                while (stream.Read(buffer, 0, buffer.Length) > 0)
                {
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static long TryReadGZipUncompressedSize(FileStream fs)
        {
            if (fs.Length < 4)
            {
                return -1;
            }

            long current = fs.Position;
            fs.Seek(-4, SeekOrigin.End);
            byte[] sizeBytes = new byte[4];
            int read = fs.Read(sizeBytes, 0, sizeBytes.Length);
            fs.Seek(current, SeekOrigin.Begin);
            return read == 4 ? BitConverter.ToUInt32(sizeBytes, 0) : -1;
        }

        private readonly record struct TarEntryInfo(string Name, long Size, string Type, DateTimeOffset Modified);

        private enum TarCompression
        {
            None,
            Gzip
        }
    }

    internal enum ArchiveFormat
    {
        None,
        Zip,
        Tar,
        TarGz,
        GZipSingle
    }

    internal sealed class ArchiveSummaryResult
    {
        public ArchiveSummaryResult(string summary, TableSummary? table = null, TextPreview? preview = null)
        {
            Summary = summary;
            Table = table;
            Preview = preview;
        }

        public string Summary { get; }
        public TableSummary? Table { get; }
        public TextPreview? Preview { get; }
    }
}

