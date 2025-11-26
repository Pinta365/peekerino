using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Peekerino.Services
{
    internal static class BinarySummaryService
    {
        private static readonly (byte[] Signature, string Description)[] KnownSignatures =
        {
            (AsBytes("4D5A"), "Windows Executable (PE)"),
            (AsBytes("7F454C46"), "ELF Executable"),
            (AsBytes("504B0304"), "ZIP archive"),
            (AsBytes("89504E47"), "PNG image"),
            (AsBytes("25504446"), "PDF document"),
            (AsBytes("47494638"), "GIF image"),
            (AsBytes("424D"), "BMP image"),
            (AsBytes("494433"), "MP3 audio (ID3)")
        };

        public static string Summarize(string path, Configuration.PeekerinoOptions.SummaryOptions options)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var summary = new StringBuilder();

                byte[] head = ReadBytes(fs, options.BinaryHeaderBytes);
                fs.Seek(0, SeekOrigin.Begin);
                string detectedType = DetectFileType(head);
                if (!string.IsNullOrEmpty(detectedType))
                {
                    summary.AppendLine($"Detected format: {detectedType}");
                }

                string sha256 = ComputeSha256(fs);
                summary.AppendLine($"SHA256: {sha256}");

                fs.Seek(0, SeekOrigin.Begin);
                double entropy = CalculateEntropy(fs, headBytesToUse: options.BinaryEntropySampleBytes);
                summary.AppendLine($"Entropy (0-8): {entropy:F2}");

                summary.AppendLine();
                summary.AppendLine("Header (first 64 bytes):");
                summary.AppendLine(FormatHexDump(head, 0, Math.Min(head.Length, 64)));

                var strings = ExtractPrintableStrings(head.Concat(ReadBytes(fs, options.BinaryStringScanBytes)).ToArray(), minLength: options.BinaryMinStringLength, maxLength: options.BinaryMaxStringLength)
                              .Take(options.BinaryStringSampleCount)
                              .ToList();
                if (strings.Count > 0)
                {
                    summary.AppendLine();
                    summary.AppendLine("Printable samples:");
                    foreach (var s in strings)
                    {
                        summary.AppendLine($"- {s}");
                    }
                }

                return summary.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                return $"Binary summary failed: {ex.Message}";
            }
        }

        private static byte[] ReadBytes(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int read = stream.Read(buffer, 0, count);
            if (read == count)
            {
                return buffer;
            }

            Array.Resize(ref buffer, read);
            return buffer;
        }

        private static string DetectFileType(byte[] head)
        {
            foreach (var (signature, description) in KnownSignatures)
            {
                if (head.Length >= signature.Length && head.Take(signature.Length).SequenceEqual(signature))
                {
                    return description;
                }
            }

            return string.Empty;
        }

        private static string ComputeSha256(Stream stream)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static double CalculateEntropy(Stream stream, int headBytesToUse)
        {
            stream.Seek(0, SeekOrigin.Begin);
            byte[] buffer = ReadBytes(stream, headBytesToUse);
            if (buffer.Length == 0)
            {
                return 0;
            }

            var counts = new int[256];
            foreach (byte b in buffer)
            {
                counts[b]++;
            }

            double entropy = 0;
            double length = buffer.Length;
            foreach (int count in counts)
            {
                if (count == 0)
                {
                    continue;
                }

                double p = count / length;
                entropy -= p * (Math.Log(p) / Math.Log(2));
            }

            return entropy;
        }

        private static string FormatHexDump(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < length; i += 16)
            {
                int chunkSize = Math.Min(16, length - i);
                var bytes = new byte[chunkSize];
                Array.Copy(data, offset + i, bytes, 0, chunkSize);

                string hex = string.Join(" ", bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
                string ascii = new string(bytes.Select(b => b >= 32 && b <= 126 ? (char)b : '.').ToArray());
                sb.AppendLine($"{i:X4}: {hex.PadRight(16 * 3)} | {ascii}");
            }

            return sb.ToString().TrimEnd();
        }

        private static IEnumerable<string> ExtractPrintableStrings(byte[] data, int minLength, int maxLength)
        {
            var builder = new StringBuilder();
            foreach (byte b in data)
            {
                if (b >= 32 && b <= 126)
                {
                    if (builder.Length < maxLength)
                    {
                        builder.Append((char)b);
                    }
                }
                else
                {
                    if (builder.Length >= minLength)
                    {
                        yield return builder.ToString();
                    }

                    builder.Clear();
                }
            }

            if (builder.Length >= minLength)
            {
                yield return builder.ToString();
            }
        }

        private static byte[] AsBytes(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                hex = "0" + hex;
            }

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}

