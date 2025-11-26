using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Peekerino.Services
{
    public static class XmlSummarizer
    {
        public static string Summarize(string path, CancellationToken ct = default)
        {
            if (IncaDocumentSummarizer.TrySummarize(path, ct, out var specialized))
            {
                return specialized;
            }

            var sb = new StringBuilder();
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var settings = new XmlReaderSettings
                {
                    IgnoreComments = true,
                    IgnoreProcessingInstructions = true,
                    IgnoreWhitespace = true,
                    DtdProcessing = DtdProcessing.Ignore
                };

                using var reader = XmlReader.Create(fs, settings);

                string? rootName = null;
                long elementCount = 0;
                long attributeCount = 0;
                const long maxElementsToScan = 20000;
                var topElementCounts = new Dictionary<string, int>(StringComparer.Ordinal);

                while (reader.Read())
                {
                    if (ct.IsCancellationRequested)
                    {
                        ct.ThrowIfCancellationRequested();
                    }

                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        elementCount++;
                        rootName ??= reader.Name;

                        if (reader.HasAttributes)
                        {
                            attributeCount += reader.AttributeCount;
                        }

                        if (topElementCounts.TryGetValue(reader.Name, out int current))
                        {
                            topElementCounts[reader.Name] = current + 1;
                        }
                        else
                        {
                            topElementCounts[reader.Name] = 1;
                        }

                        if (elementCount >= maxElementsToScan)
                        {
                            sb.AppendLine($"(Scanned {elementCount:N0} elements — stopping early for performance)");
                            break;
                        }
                    }
                }

                sb.AppendLine($"Root element: {rootName ?? "(unknown)"}");
                sb.AppendLine($"Elements scanned: {elementCount:N0}");
                sb.AppendLine($"Attributes scanned: {attributeCount:N0}");

                if (topElementCounts.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Top element names (sample):");
                    int shown = 0;
                    foreach (var kv in topElementCounts)
                    {
                        sb.AppendLine($"  {kv.Key}: {kv.Value:N0}");
                        shown++;
                        if (shown >= 12)
                        {
                            break;
                        }
                    }
                }

                fs.Seek(0, SeekOrigin.Begin);
                using var reader2 = XmlReader.Create(fs, settings);
                int textSamplesFound = 0;
                sb.AppendLine();
                sb.AppendLine("Text samples:");
                while (reader2.Read() && textSamplesFound < 3)
                {
                    if (reader2.NodeType == XmlNodeType.Text || reader2.NodeType == XmlNodeType.CDATA)
                    {
                        var text = reader2.Value?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            sb.AppendLine($"- {TruncateOneLine(text, 200)}");
                            textSamplesFound++;
                        }
                    }
                }
            }
            catch (XmlException xex)
            {
                sb.AppendLine("XML parsing error: " + xex.Message);
            }
            catch (Exception ex)
            {
                sb.AppendLine("Error summarizing XML: " + ex.Message);
            }

            return sb.ToString();
        }

        private static string TruncateOneLine(string text, int max)
        {
            var singleLine = text.Replace("\r", " ").Replace("\n", " ");
            if (singleLine.Length <= max)
            {
                return singleLine;
            }

            return singleLine.Substring(0, max) + "…";
        }
    }
}

