using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Peekerino.Configuration;
using Peekerino.Services.Summaries;

namespace Peekerino.Services
{
    internal static class MarkdownSummaryService
    {
        private const int MaxHeadingRows = 200;

        internal static MarkdownSummaryResult Summarize(
            string path,
            PeekerinoOptions.SummaryOptions options,
            CancellationToken cancellationToken)
        {
            int previewLimit = Math.Max(1, options.MarkdownMaxCharacters);
            var previewBuilder = new StringBuilder(previewLimit);
            var headings = new List<(int Level, string Text)>();

            long totalCharacters = 0;
            int lineCount = 0;
            long wordCount = 0;
            bool truncated = false;
            bool inCodeBlock = false;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lineCount++;
                totalCharacters += line.Length + 1; // +1 for newline
                wordCount += CountWords(line);

                string headingSource = line.TrimStart();
                if (TryParseHeading(headingSource, out int level, out string headingText))
                {
                    headings.Add((level, headingText));
                }

                if (!truncated)
                {
                    foreach (string formatted in FormatMarkdownLine(line, ref inCodeBlock))
                    {
                        AppendLineLimited(previewBuilder, formatted, previewLimit, ref truncated);
                        if (truncated)
                        {
                            break;
                        }
                    }
                }
            }

            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine($"Lines: {lineCount:N0}");
            summaryBuilder.AppendLine($"Words: {wordCount:N0}");
            summaryBuilder.AppendLine($"Characters (including newline markers): {totalCharacters:N0}");
            summaryBuilder.AppendLine($"Headings detected: {headings.Count:N0}");

            if (truncated)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"Preview limited to first {previewLimit:N0} characters.");
            }

            if (headings.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("Headings:");
                int limit = Math.Min(headings.Count, MaxHeadingRows);
                for (int i = 0; i < limit; i++)
                {
                    var (level, text) = headings[i];
                    string indent = level > 1
                        ? new string(' ', Math.Min((level - 1) * 2, 10))
                        : string.Empty;
                    summaryBuilder.AppendLine($"{indent}- {text}");
                }

                if (headings.Count > limit)
                {
                    summaryBuilder.AppendLine($"... ({headings.Count - limit} more)");
                }
            }

            var preview = new TextPreview(Path.GetFileName(path), previewBuilder.ToString(), truncated);
            return new MarkdownSummaryResult(summaryBuilder.ToString(), preview);
        }

        private static IReadOnlyList<string> FormatMarkdownLine(string line, ref bool inCodeBlock)
        {
            var formatted = new List<string>();
            string trimmedEnd = line.TrimEnd();
            string trimmedStart = trimmedEnd.TrimStart();

            if (trimmedStart.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                formatted.Add(inCodeBlock ? "[code block]" : string.Empty);
                return formatted;
            }

            if (inCodeBlock)
            {
                formatted.Add($"    {trimmedEnd}");
                return formatted;
            }

            if (TryParseHeading(trimmedStart, out int level, out string headingText))
            {
                string inlineHeading = FormatInline(headingText);

                if (level == 1)
                {
                    string upper = inlineHeading.ToUpperInvariant();
                    formatted.Add(upper);
                    formatted.Add(new string('=', Math.Max(3, Math.Min(upper.Length, 80))));
                }
                else if (level == 2)
                {
                    formatted.Add(inlineHeading);
                    formatted.Add(new string('-', Math.Max(3, Math.Min(inlineHeading.Length, 80))));
                }
                else
                {
                    string indent = new string(' ', Math.Min((level - 2) * 2, 6));
                    formatted.Add($"{indent}• {inlineHeading}");
                }

                return formatted;
            }

            if (trimmedStart.StartsWith(">"))
            {
                string content = trimmedStart.TrimStart('>', ' ');
                formatted.Add($"│ {FormatInline(content)}");
                return formatted;
            }

            if (IsHorizontalRule(trimmedStart))
            {
                formatted.Add(new string('─', 40));
                return formatted;
            }

            if (IsListItem(trimmedStart, out string marker, out string itemContent))
            {
                string formattedItem = FormatInline(itemContent);
                formatted.Add($"{marker} {formattedItem}");
                return formatted;
            }

            formatted.Add(FormatInline(trimmedEnd));
            return formatted;
        }

        private static void AppendLineLimited(StringBuilder builder, string content, int limit, ref bool truncated)
        {
            if (truncated)
            {
                return;
            }

            if (builder.Length > 0)
            {
                AppendLimited(builder, Environment.NewLine, limit, ref truncated);
                if (truncated)
                {
                    return;
                }
            }

            AppendLimited(builder, content, limit, ref truncated);
        }

        private static void AppendLimited(StringBuilder builder, string content, int limit, ref bool truncated)
        {
            if (truncated)
            {
                return;
            }

            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            foreach (char c in content)
            {
                if (builder.Length >= limit)
                {
                    truncated = true;
                    return;
                }

                builder.Append(c);
            }

            if (builder.Length >= limit)
            {
                truncated = true;
            }
        }

        private static bool IsHorizontalRule(string line)
        {
            if (line.Length < 3)
            {
                return false;
            }

            int nonSpaceCount = 0;
            char previous = '\0';

            foreach (char c in line)
            {
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                if (c is '-' or '_' or '*')
                {
                    if (previous != '\0' && c != previous)
                    {
                        return false;
                    }

                    previous = c;
                    nonSpaceCount++;
                    continue;
                }

                return false;
            }

            return nonSpaceCount >= 3;
        }

        private static bool IsListItem(string line, out string marker, out string content)
        {
            marker = string.Empty;
            content = string.Empty;

            if (line.Length >= 2 && (line[0] is '-' or '*' or '+') && line[1] == ' ')
            {
                marker = "•";
                content = line[2..].Trim();
                return true;
            }

            int dotIndex = line.IndexOf('.');
            if (dotIndex > 0 && dotIndex + 1 < line.Length && line[dotIndex + 1] == ' ')
            {
                bool allDigits = true;
                for (int i = 0; i < dotIndex; i++)
                {
                    if (!char.IsDigit(line[i]))
                    {
                        allDigits = false;
                        break;
                    }
                }

                if (allDigits)
                {
                    marker = line[..dotIndex] + ".";
                    content = line[(dotIndex + 2)..].Trim();
                    return true;
                }
            }

            return false;
        }

        private static string FormatInline(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string result = ReplaceLinks(text);
            result = ReplaceInlineCode(result);
            result = result.Replace("**", string.Empty, StringComparison.Ordinal);
            result = result.Replace("__", string.Empty, StringComparison.Ordinal);
            result = result.Replace("~~", string.Empty, StringComparison.Ordinal);
            return result.Trim();
        }

        private static string ReplaceLinks(string text)
        {
            var builder = new StringBuilder(text.Length);
            int index = 0;

            while (index < text.Length)
            {
                int open = text.IndexOf('[', index);
                if (open == -1)
                {
                    builder.Append(text, index, text.Length - index);
                    break;
                }

                builder.Append(text, index, open - index);
                int closeBracket = text.IndexOf(']', open + 1);
                if (closeBracket == -1)
                {
                    builder.Append(text, open, text.Length - open);
                    break;
                }

                if (closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
                {
                    int closeParen = text.IndexOf(')', closeBracket + 2);
                    if (closeParen != -1)
                    {
                        string label = text.Substring(open + 1, closeBracket - open - 1);
                        string url = text.Substring(closeBracket + 2, closeParen - closeBracket - 2);
                        builder.Append(label);
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            builder.Append(" (");
                            builder.Append(url);
                            builder.Append(')');
                        }

                        index = closeParen + 1;
                        continue;
                    }
                }

                builder.Append(text, open, closeBracket - open + 1);
                index = closeBracket + 1;
            }

            return builder.ToString();
        }

        private static string ReplaceInlineCode(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains('`'))
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            bool inCode = false;

            foreach (char c in text)
            {
                if (c == '`')
                {
                    builder.Append('\'');
                    inCode = !inCode;
                    continue;
                }

                builder.Append(c);
            }

            if (inCode)
            {
                builder.Append('\'');
            }

            return builder.ToString();
        }

        private static bool TryParseHeading(string line, out int level, out string text)
        {
            level = 0;
            text = string.Empty;

            int index = 0;
            while (index < line.Length && line[index] == '#')
            {
                level++;
                index++;
            }

            if (level == 0 || level > 6)
            {
                return false;
            }

            if (index < line.Length && line[index] == ' ')
            {
                index++;
            }

            text = line[index..].Trim();
            return text.Length > 0;
        }

        private static long CountWords(string line)
        {
            long count = 0;
            bool inWord = false;

            foreach (char c in line)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (!inWord)
                    {
                        inWord = true;
                        count++;
                    }
                }
                else
                {
                    inWord = false;
                }
            }

            return count;
        }
    }

    internal sealed class MarkdownSummaryResult
    {
        public MarkdownSummaryResult(string summary, TextPreview preview)
        {
            Summary = summary;
            Preview = preview;
        }

        public string Summary { get; }
        public TextPreview Preview { get; }
    }
}

