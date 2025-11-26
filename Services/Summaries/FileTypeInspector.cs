using System;
using System.IO;
using System.Text;
using Peekerino.Configuration;

namespace Peekerino.Services.Summaries
{
    internal static class FileTypeInspector
    {
        public static bool LooksLikeXml(FileSummaryContext context)
        {
            if (string.Equals(context.Extension, ".xml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return LooksLikeXml(context.Path);
        }

        public static bool LooksLikeJson(FileSummaryContext context)
        {
            if (string.Equals(context.Extension, ".json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return LooksLikeJson(context.Path);
        }

        public static ArchiveFormat DetectArchiveFormat(FileSummaryContext context)
        {
            string lower = context.Path.ToLowerInvariant();

            if (lower.EndsWith(".tar.gz", StringComparison.Ordinal) || lower.EndsWith(".tgz", StringComparison.Ordinal))
            {
                return ArchiveFormat.TarGz;
            }

            if (lower.EndsWith(".tar", StringComparison.Ordinal))
            {
                return ArchiveFormat.Tar;
            }

            if (lower.EndsWith(".gz", StringComparison.Ordinal))
            {
                return ArchiveFormat.GZipSingle;
            }

            if (lower.EndsWith(".zip", StringComparison.Ordinal) || HasZipSignature(context.Path))
            {
                return ArchiveFormat.Zip;
            }

            return ArchiveFormat.None;
        }

        public static bool IsProbablyTextFile(FileSummaryContext context)
        {
            try
            {
                using var fs = new FileStream(context.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                int sampleSize = Math.Min(context.Options.Summary.TextPreviewBytes, (int)Math.Min(context.Options.Summary.TextPreviewBytes, fs.Length));
                var buffer = new byte[sampleSize];
                int read = fs.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    return true;
                }

                if (LooksLikePdfHeader(buffer))
                {
                    return false;
                }

                int nonTextCount = 0;
                for (int i = 0; i < read; i++)
                {
                    byte b = buffer[i];
                    if (b == 0)
                    {
                        return false;
                    }

                    if (b < 0x09 || (b > 0x0D && b < 0x20))
                    {
                        nonTextCount++;
                    }
                }

                return nonTextCount <= read * 0.05;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool LooksLikeXml(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                var head = new char[256];
                int read = reader.Read(head, 0, head.Length);
                var text = new string(head, 0, Math.Max(0, read)).TrimStart();
                return text.StartsWith("<", StringComparison.Ordinal) ||
                       text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool LooksLikeJson(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                int value;
                do
                {
                    value = reader.Read();
                } while (value != -1 && char.IsWhiteSpace((char)value));

                if (value == -1)
                {
                    return false;
                }

                char first = (char)value;
                return first is '{' or '[';
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool LooksLikePdfHeader(byte[] buffer)
        {
            return buffer.Length >= 4 &&
                   buffer[0] == 0x25 &&
                   buffer[1] == 0x50 &&
                   buffer[2] == 0x44 &&
                   buffer[3] == 0x46;
        }

        private static bool HasZipSignature(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] signature = new byte[4];
                int read = fs.Read(signature, 0, signature.Length);
                return read == 4 && signature[0] == 0x50 && signature[1] == 0x4B && (signature[2] == 0x03 || signature[2] == 0x05 || signature[2] == 0x07);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}

